using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Vulthil.SharedKernel.Application.Data;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

namespace Vulthil.SharedKernel.Infrastructure;

/// <summary>
/// Provides extension methods for registering infrastructure-layer services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers a <see cref="BaseDbContext"/>-derived context with unit-of-work and optional outbox processing.
    /// </summary>
    /// <typeparam name="TDbContext">The concrete DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="databaseInfrastructureConfiguratorAction">An action to configure the database infrastructure.</param>
    /// <returns>The service collection for chaining.</returns>
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

    /// <summary>
    /// Registers a <see cref="BaseDbContext"/>-derived context with an additional service interface and optional outbox processing.
    /// </summary>
    /// <typeparam name="TDbContextInterface">The service interface to register the context as.</typeparam>
    /// <typeparam name="TDbContext">The concrete DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="databaseInfrastructureConfiguratorAction">An action to configure the database infrastructure.</param>
    /// <returns>The service collection for chaining.</returns>
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

    /// <summary>
    /// Ensures the database for <typeparamref name="TDbContext"/> has been created.
    /// </summary>
    /// <typeparam name="TDbContext">The DbContext type.</typeparam>
    /// <param name="host">The host whose services to use.</param>
    public static Task EnsureCreatedAsync<TDbContext>(this IHost host)
       where TDbContext : DbContext
       => host.Services.EnsureCreatedAsync<TDbContext>();

    /// <summary>
    /// Ensures the database for <typeparamref name="TDbContext"/> has been created using the provided service provider.
    /// </summary>
    /// <typeparam name="TDbContext">The DbContext type.</typeparam>
    /// <param name="services">The service provider to resolve the context from.</param>
    public static async Task EnsureCreatedAsync<TDbContext>(this IServiceProvider services)
        where TDbContext : DbContext
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Applies any pending EF Core migrations for <typeparamref name="TDbContext"/>.
    /// </summary>
    /// <typeparam name="TDbContext">The DbContext type.</typeparam>
    /// <param name="host">The host whose services to use.</param>
    public static Task MigrateAsync<TDbContext>(this IHost host)
        where TDbContext : DbContext
        => host.Services.MigrateAsync<TDbContext>();

    /// <summary>
    /// Applies any pending EF Core migrations for <typeparamref name="TDbContext"/> using the provided service provider.
    /// </summary>
    /// <typeparam name="TDbContext">The DbContext type.</typeparam>
    /// <param name="services">The service provider to resolve the context from.</param>
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
