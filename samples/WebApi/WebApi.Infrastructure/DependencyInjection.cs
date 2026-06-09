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
using WebApi.Application.SideEffects.Create;
using WebApi.Infrastructure.Data;

namespace WebApi.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddDatabaseInfrastructure(this IHostApplicationBuilder builder, string connectionStringKey)
    {
        builder
            .AddDbContext<IWebApiDbContext, WebApiDbContext>(databaseInfrastructureConfigurator =>
                    databaseInfrastructureConfigurator
                        .UseNpgsql(connectionStringKey)
                        .EnableOutboxProcessing());

        builder.Services.AddRelationalInbox<WebApiDbContext>();

        return builder;
    }

    public static Task MigrateAsync(this IHost host) => host.MigrateAsync<WebApiDbContext>();

    public static IHostApplicationBuilder AddRabbitMqMessagingInfrastructure(this IHostApplicationBuilder builder, string rabbitMqConnectionStringKey)
    {
        builder.AddMessaging(x =>
        {
            x.ConfigureMessage<MainEntityCreatedIntegrationEvent>(pd => pd.UseRoutingKey("main-entity.created"));

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

            x.AddIdempotentInbox<MainEntityCreatedIntegrationEvent>(context => context.Message.Id.ToString());

            x.AddTransactionalOutbox();

            x.UseRabbitMq(rabbitMqConnectionStringKey);
        });


        return builder;
    }
}
