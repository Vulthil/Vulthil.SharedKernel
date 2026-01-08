using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Messaging;
using Vulthil.Messaging.RabbitMq;
using Vulthil.SharedKernel.Infrastructure;
using WebApi.Application;
using WebApi.Application.MainEntities.Create;
using WebApi.Application.SideEffects.Create;
using WebApi.Infrastructure.Data;

namespace WebApi.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddDatabaseInfrastructure(this IHostApplicationBuilder builder, string connectionStringKey)
    {
        builder.Services
            .AddDbContext<IWebApiDbContext, WebApiDbContext>(databaseInfrastructureConfigurator =>
                    databaseInfrastructureConfigurator
                        .ConfigureDbContextOptions(options =>
                            options.UseNpgsql(builder.Configuration.GetConnectionString(connectionStringKey)))
                        .EnableOutboxProcessing());

        builder.EnrichNpgsqlDbContext<WebApiDbContext>(
            configureSettings: settings =>
            {
                settings.DisableRetry = true;
                settings.CommandTimeout = 30;
            });

        return builder;
    }

    public static Task MigrateAsync(this IHost host) => host.MigrateAsync<WebApiDbContext>();

    public static IHostApplicationBuilder AddRabbitMqMessagingInfrastructure(this IHostApplicationBuilder builder, string rabbitMqConnectionStringKey)
    {
        builder.Services.AddMessaging(builder.Configuration, x =>
        {
            x.AddQueue("main-entity-events", queue =>
            {
                queue.AddConsumer<MainEntityCreatedIntegrationEventConsumer>();
            });

            x.AddEvent<MainEntityCreatedIntegrationEvent>();

            x.UseRabbitMq();
        });

        builder.AddRabbitMqClient(rabbitMqConnectionStringKey);

        return builder;
    }
}
