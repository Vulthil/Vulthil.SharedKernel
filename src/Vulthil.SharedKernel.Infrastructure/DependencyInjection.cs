using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Vulthil.SharedKernel.Application.Data;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

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
    public static IHostApplicationBuilder AddDbContext<TDbContext>(this IHostApplicationBuilder hostApplicationBuilder, Action<IDatabaseInfrastructureConfigurator<TDbContext>> databaseInfrastructureConfiguratorAction)
        where TDbContext : BaseDbContext
    {
        var databaseInfrastructureConfigurator = new DatabaseInfrastructureConfigurator<TDbContext>(hostApplicationBuilder);
        databaseInfrastructureConfiguratorAction(databaseInfrastructureConfigurator);
        databaseInfrastructureConfigurator.FinalizeConfiguration();

        var dbContextLifetime = databaseInfrastructureConfigurator.DbContextLifetime;

        hostApplicationBuilder.Services.Add(new ServiceDescriptor(
            typeof(IUnitOfWork),
            sp => sp.GetRequiredService<TDbContext>(),
            dbContextLifetime));

        if (databaseInfrastructureConfigurator.OutboxProcessingEnabled)
        {
            hostApplicationBuilder.Services.AddOutboxProcessing(databaseInfrastructureConfigurator);
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

    public static IHostApplicationBuilder AddDbContext<TDbContextInterface, TDbContext>(this IHostApplicationBuilder hostApplicationBuilder, Action<IDatabaseInfrastructureConfigurator<TDbContext>> databaseInfrastructureConfiguratorAction)
        where TDbContext : BaseDbContext, TDbContextInterface
        where TDbContextInterface : class
    {
        hostApplicationBuilder.AddDbContext(databaseInfrastructureConfiguratorAction);

        var dbContextLifetime = FindLifetime(hostApplicationBuilder.Services, typeof(TDbContext));

        hostApplicationBuilder.Services.Add(new ServiceDescriptor(
            typeof(TDbContextInterface),
            sp => sp.GetRequiredService<TDbContext>(),
            dbContextLifetime));

        return hostApplicationBuilder;
    }

    private static void AddOutboxProcessing<TDbContext>(this IServiceCollection services, DatabaseInfrastructureConfigurator<TDbContext> configurator)
        where TDbContext : DbContext, ISaveOutboxMessages
    {
        var dbContextLifetime = configurator.DbContextLifetime;

        // The engine (options, signal, processor, background service, in-process domain-event sink) registers its own
        // internals; the EF-specific store, capture interceptor, and context marker are wired here.
        services.AddOutboxEngine(configurator.OutboxOptionsAction, dbContextLifetime);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IOutboxInterceptor, DomainEventsToOutboxMessageSaveChangesInterceptor>());

        services.Add(new ServiceDescriptor(
            typeof(ISaveOutboxMessages),
            sp => sp.GetRequiredService<TDbContext>(),
            dbContextLifetime));

        var storeType = configurator.OutboxStoreType.IsGenericTypeDefinition
            ? configurator.OutboxStoreType.MakeGenericType(typeof(TDbContext))
            : configurator.OutboxStoreType;

        services.Add(new ServiceDescriptor(typeof(IOutboxStore), storeType, dbContextLifetime));
    }

    /// <summary>
    /// Returns the <see cref="ServiceLifetime"/> of the last descriptor in <paramref name="services"/>
    /// whose <see cref="ServiceDescriptor.ServiceType"/> equals <paramref name="serviceType"/>, or
    /// <see cref="ServiceLifetime.Scoped"/> if no such descriptor exists. Walking in reverse mirrors
    /// the DI container's "last registration wins" rule when resolving a single service.
    /// </summary>
    internal static ServiceLifetime FindLifetime(IServiceCollection services, Type serviceType)
    {
        for (var i = services.Count - 1; i >= 0; i--)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType == serviceType)
            {
                return descriptor.Lifetime;
            }
        }
        return ServiceLifetime.Scoped;
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
