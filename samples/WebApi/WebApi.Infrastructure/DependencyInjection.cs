using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging;
using Vulthil.Messaging.Inbox;
using Vulthil.Messaging.Inbox.Relational;
using Vulthil.Messaging.Outbox;
using Vulthil.Messaging.RabbitMq;
using Vulthil.SharedKernel.Infrastructure;
using Vulthil.SharedKernel.Infrastructure.Npgsql;
using Vulthil.SharedKernel.Infrastructure.Relational;
using WebApi.Application;
using WebApi.Application.MainEntities.Create;
using WebApi.Application.Shipping;
using WebApi.Application.SideEffects.Create;
using WebApi.Infrastructure.Data;
using WebApi.Infrastructure.Messaging;

namespace WebApi.Infrastructure;

public static class DependencyInjection
{
    public const string ShipmentEventsQueueName = "webapi-shipment-events";

    public static IHostApplicationBuilder AddDatabaseInfrastructure(this IHostApplicationBuilder builder, string connectionStringKey)
    {
        builder
            .AddDbContext<IWebApiDbContext, WebApiDbContext>(databaseInfrastructureConfigurator =>
                    databaseInfrastructureConfigurator
                        .UseNpgsql(connectionStringKey, settings => settings.CommandTimeout = 30)
                        .EnableOutboxProcessing(o =>
                        {
                            o.BatchSize = 20;
                            o.MaxRetries = 5;
                            o.Retention.Enabled = true;
                        }));

        builder.Services.AddRelationalInbox<WebApiDbContext>(o => o.Retention.Enabled = true);

        return builder;
    }

    public static Task MigrateAsync(this IHost host) => host.MigrateAsync<WebApiDbContext>();

    public static IHostApplicationBuilder AddRabbitMqMessagingInfrastructure(this IHostApplicationBuilder builder, string rabbitMqConnectionStringKey)
    {
        builder.AddMessaging(x =>
        {
            x.ConfigureMessage<MainEntityCreatedIntegrationEvent>(pd => pd
                .UseRoutingKey("main-entity.created")
                .UseCorrelationId(e => e.Id.ToString()));

            ConfigureShipping(x);

            x.ConfigureQueue("MainEntityEvents", queue =>
            {
                queue.UseRetry(r => r.SetIntervals(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));

                queue.UseDeadLetterQueue();

                queue.AddRequestConsumer<SideEffectRequestConsumer>();

                queue.Subscribe<MainEntityCreatedIntegrationEvent>("main-entity.created");
                queue.Subscribe<MainEntityCreatedIntegrationEvent>("main-entity.created2");
                queue.AddConsumer<MainEntityCreatedIntegrationEventConsumer>(c =>
                {
                    c.UseRetry(r => r.Immediate(5));
                });
            });

            x.AddOpenConsumeFilter(typeof(LoggingScopeConsumeFilter<>));
            x.AddConsumeFilter<ShipmentDeliveredValidationFilter>();

            x.AddIdempotentInbox<MainEntityCreatedIntegrationEvent>(context => context.Message.Id.ToString());
            x.AddIdempotentInbox<ShipmentDelivered>(context => context.Message.ShipmentId.ToString());

            // Publish filters run in registration order, and the outbox filter captures a message without calling
            // the rest of the pipeline; the relay later sends the captured context as it is. The header filter is
            // registered first so the header is part of what the outbox captures.
            x.AddPublishFilter<SourceServiceHeaderPublishFilter>();
            x.AddTransactionalOutbox();

            // Run the consumer in a transaction so any messages it publishes are captured by the outbox. Here it
            // composes with the inbox (which opens the transaction first) — the filter joins rather than nesting.
            x.AddTransactionalConsumer<MainEntityCreatedIntegrationEvent>();

            x.UseRabbitMq(rabbitMqConnectionStringKey);
        });


        return builder;
    }

    private static void ConfigureShipping(IMessagingConfigurator messaging)
    {
        // The Shipments sample owns these contracts; this service shares no assembly with it, so the URN (and, for
        // the published event, the exchange) is what both services agree on.
        messaging.ConfigureMessage<CreateShipment>(message => message.Urn = new Uri(CreateShipment.Urn));
        messaging.ConfigureMessage<ShipmentDelivered>(message =>
        {
            message.Urn = new Uri(ShipmentDelivered.Urn);
            message.Exchange = ShipmentDelivered.Exchange;
            message.ExchangeType = MessagingExchangeType.Topic;
        });

        messaging.ConfigureQueue(ShipmentEventsQueueName, queue =>
        {
            queue.Subscribe<ShipmentDelivered>("shipment.delivered.#");
            queue.UseRetry(retry =>
            {
                retry.Exponential(5, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(10));
                retry.UseJitter(0.2);
                retry.InMemory();
            });
            queue.UseDeadLetterQueue();
            queue.AddConsumer<ShipmentDeliveredConsumer>();
        });
    }
}
