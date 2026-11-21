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
using Orders.Application;
using Orders.Application.Orders.Place;
using Orders.Application.Shipping;
using Orders.Application.Fulfillments.GetByOrder;
using Orders.Application.Fulfillments.Start;
using Orders.Infrastructure.Data;
using Orders.Infrastructure.Messaging;

namespace Orders.Infrastructure;

public static class DependencyInjection
{
    public const string OrderEventsQueueName = "OrderEvents";

    public const string ShipmentEventsQueueName = "orders-shipment-events";

    public static IHostApplicationBuilder AddOrdersPersistence(this IHostApplicationBuilder builder, string connectionStringKey)
    {
        builder
            .AddDbContext<IOrdersDbContext, OrdersDbContext>(databaseInfrastructureConfigurator =>
                    databaseInfrastructureConfigurator
                        .UseNpgsql(connectionStringKey, settings => settings.CommandTimeout = 30)
                        .EnableOutboxProcessing(o =>
                        {
                            o.BatchSize = 20;
                            o.MaxRetries = 5;
                            o.Retention.Enabled = true;
                        }));

        builder.Services.AddRelationalInbox<OrdersDbContext>(o => o.Retention.Enabled = true);

        return builder;
    }

    public static Task MigrateAsync(this IHost host) => host.MigrateAsync<OrdersDbContext>();

    public static IHostApplicationBuilder AddOrdersMessaging(this IHostApplicationBuilder builder, string rabbitMqConnectionStringKey)
    {
        builder.AddMessaging(x =>
        {
            x.ConfigureMessage<OrderPlacedIntegrationEvent>(pd => pd
                .UseRoutingKey("order.placed")
                .UseCorrelationId(e => e.OrderId.ToString()));

            ConfigureShipping(x);

            x.ConfigureQueue(OrderEventsQueueName, queue =>
            {
                queue.UseRetry(r => r.SetIntervals(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));

                queue.UseDeadLetterQueue();

                queue.AddRequestConsumer<OrderFulfillmentsRequestConsumer>();

                queue.Subscribe<OrderPlacedIntegrationEvent>("order.placed");
                queue.Subscribe<OrderPlacedIntegrationEvent>("order.placed.priority");
                queue.AddConsumer<StartFulfillmentConsumer>(c =>
                {
                    c.UseRetry(r => r.Immediate(5));
                });
            });

            x.AddOpenConsumeFilter(typeof(LoggingScopeConsumeFilter<>));
            x.AddConsumeFilter<ShipmentDeliveredValidationFilter>();

            x.AddIdempotentInbox<OrderPlacedIntegrationEvent>(context => context.Message.OrderId.ToString());
            x.AddIdempotentInbox<ShipmentDelivered>(context => context.Message.ShipmentId.ToString());

            // Publish filters run in registration order, and the outbox filter captures a message without calling
            // the rest of the pipeline; the relay later sends the captured context as it is. The header filter is
            // registered first so the header is part of what the outbox captures.
            x.AddPublishFilter<SourceServiceHeaderPublishFilter>();
            x.AddTransactionalOutbox();

            // Run the consumer in a transaction so any messages it publishes are captured by the outbox. Here it
            // composes with the inbox (which opens the transaction first) — the filter joins rather than nesting.
            x.AddTransactionalConsumer<OrderPlacedIntegrationEvent>();

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
