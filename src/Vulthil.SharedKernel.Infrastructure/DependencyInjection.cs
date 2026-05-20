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
    /// <param name="hostApplicationBuilder">The host application builder.</param>
    /// <param name="databaseInfrastructureConfiguratorAction">An action to configure the database infrastructure.</param>
    /// <returns>The host application builder for chaining.</returns>
    public static IHostApplicationBuilder AddDbContext<TDbContext>(this IHostApplicationBuilder hostApplicationBuilder, Action<IDatabaseInfrastructureConfigurator> databaseInfrastructureConfiguratorAction)
        where TDbContext : BaseDbContext
    {
        var databaseInfrastructureConfigurator = new DatabaseInfrastructureConfigurator(hostApplicationBuilder);
        databaseInfrastructureConfiguratorAction(databaseInfrastructureConfigurator);

        hostApplicationBuilder.Services.AddDbContext<TDbContext>(databaseInfrastructureConfigurator.OptionsBuilder);
        hostApplicationBuilder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TDbContext>());
        if (databaseInfrastructureConfigurator.OutboxProcessingEnabled)
        {
            hostApplicationBuilder.Services.AddOutboxProcessing<TDbContext>(databaseInfrastructureConfigurator);
        }

        return hostApplicationBuilder;
    }

    /// <summary>
    /// Registers a <see cref="BaseDbContext"/>-derived context with an additional service interface and optional outbox processing.
    /// </summary>
    /// <typeparam name="TDbContextInterface">The service interface to register the context as.</typeparam>
    /// <typeparam name="TDbContext">The concrete DbContext type.</typeparam>
    /// <param name="hostApplicationBuilder">The host application builder.</param>
    /// <param name="databaseInfrastructureConfiguratorAction">An action to configure the database infrastructure.</param>
    /// <returns>The host application builder for chaining.</returns>

    public static IHostApplicationBuilder AddDbContext<TDbContextInterface, TDbContext>(this IHostApplicationBuilder hostApplicationBuilder, Action<IDatabaseInfrastructureConfigurator> databaseInfrastructureConfiguratorAction)
        where TDbContext : BaseDbContext, TDbContextInterface
        where TDbContextInterface : class
    {
        hostApplicationBuilder.AddDbContext<TDbContext>(databaseInfrastructureConfiguratorAction);
        hostApplicationBuilder.Services.AddScoped<TDbContextInterface>(sp => sp.GetRequiredService<TDbContext>());

        return hostApplicationBuilder;
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
}
