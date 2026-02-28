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

/// <summary>
/// Represents the DependencyInjection.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Executes this member.
    /// </summary>
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

    /// <summary>
    /// Executes this member.
    /// </summary>
    public static Task MigrateAsync(this IHost host) => host.MigrateAsync<WebApiDbContext>();

    /// <summary>
    /// Executes this member.
    /// </summary>
    public static IHostApplicationBuilder AddRabbitMqMessagingInfrastructure(this IHostApplicationBuilder builder, string rabbitMqConnectionStringKey)
    {
        builder.AddMessaging(x =>
        {
            x.RegisterRoutingKeyFormatter<MainEntityCreatedIntegrationEvent>("main-entity.created");

            x.AddQueue("MainEntityEvents", queue =>
            {
                queue.UseRetry(r => r.SetIntervals(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)));

                queue.UseDeadLetterQueue();

                queue.AddRequestConsumer<SideEffectRequestConsumer>();

                queue.AddConsumer<MainEntityCreatedIntegrationEventConsumer>(c =>
                {
                    c.Bind<MainEntityCreatedIntegrationEvent>("main-entity.created");
                    c.UseRetry(r => r.Immediate(5));
                });
                queue.AddConsumer<MainEntityCreatedIntegrationEventConsumer>(c =>
                {
                    c.Bind<MainEntityCreatedIntegrationEvent>("main-entity.created2");
                });
            });

            x.UseRabbitMq(rabbitMqConnectionStringKey);
        });


        return builder;
    }
}
