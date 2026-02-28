using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Vulthil.SharedKernel.Application.Data;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

namespace Vulthil.SharedKernel.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDbContext<TDbContext>(this IServiceCollection services, Action<DatabaseInfrastructureConfigurator> databaseInfrastructureConfiguratorAction)
        where TDbContext : BaseDbContext
    {
        var databaseInfrastructureConfigurator = new DatabaseInfrastructureConfigurator();
        databaseInfrastructureConfiguratorAction(databaseInfrastructureConfigurator);

        services.AddDbContext<TDbContext>(databaseInfrastructureConfigurator.OptionsBuilder);
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TDbContext>());
        if (databaseInfrastructureConfigurator.OutboxProcessingEnabled)
        {
            services.AddOutboxProcessing<TDbContext>(databaseInfrastructureConfigurator);
        }

        return services;
    }

    public static IServiceCollection AddDbContext<TDbContextInterface, TDbContext>(this IServiceCollection services, Action<DatabaseInfrastructureConfigurator> databaseInfrastructureConfiguratorAction)
        where TDbContext : BaseDbContext, TDbContextInterface
        where TDbContextInterface : class
    {
        services.AddDbContext<TDbContext>(databaseInfrastructureConfiguratorAction);
        services.AddScoped<TDbContextInterface>(sp => sp.GetRequiredService<TDbContext>());

        return services;
    }

    private static IServiceCollection AddOutboxProcessing<TDbContext>(this IServiceCollection services, DatabaseInfrastructureConfigurator configurator)
        where TDbContext : ISaveOutboxMessages
    {
        var optionsAction = configurator.OutboxOptionsAction ?? (static o => { });
        services.AddOptions<OutboxProcessingOptions>()
            .Configure(optionsAction)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<DomainEventsToOutboxMessageSaveChangesInterceptor>();
        services.AddScoped<ISaveOutboxMessages>(sp => sp.GetRequiredService<TDbContext>());
        services.AddScoped(typeof(IOutboxStrategy), configurator.OutboxStrategyType);
        services.AddScoped<OutboxProcessor>();
        services.AddHostedService<OutboxBackgroundService>();

        return services;
    }

    public static Task EnsureCreatedAsync<TDbContext>(this IHost host)
       where TDbContext : DbContext
       => host.Services.EnsureCreatedAsync<TDbContext>();

    public static async Task EnsureCreatedAsync<TDbContext>(this IServiceProvider services)
        where TDbContext : DbContext
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    public static Task MigrateAsync<TDbContext>(this IHost host)
        where TDbContext : DbContext
        => host.Services.MigrateAsync<TDbContext>();

    public static async Task MigrateAsync<TDbContext>(this IServiceProvider services)
        where TDbContext : DbContext
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            await context.Database.MigrateAsync();
        }
    }
}
