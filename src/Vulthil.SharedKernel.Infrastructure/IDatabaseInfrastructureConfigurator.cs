using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

namespace Vulthil.SharedKernel.Infrastructure;

/// <summary>
/// Defines methods for configuring database infrastructure options for Entity Framework Core DbContext instances.
/// </summary>
public interface IDatabaseInfrastructureConfigurator<TDbContext>
    where TDbContext : DbContext
{
    /// <summary>
    /// Provides access to the host application builder for configuring services and settings.
    /// </summary>
    IHostApplicationBuilder HostApplicationBuilder { get; }

    /// <summary>
    /// Gets a value indicating whether outbox processing has been enabled on this configurator.
    /// Provider extensions can read this inside an <see cref="OnConfigured"/> callback to make
    /// decisions that depend on the final state of the configuration (for example, enforcing
    /// settings that are required for the outbox transaction strategy to work).
    /// </summary>
    bool OutboxProcessingEnabled { get; }

    /// <summary>
    /// Enables outbox processing with optional configuration.
    /// </summary>
    /// <param name="optionsAction">An optional action to configure outbox processing options.</param>
    /// <returns>The current configurator instance for chaining.</returns>
    IDatabaseInfrastructureConfigurator<TDbContext> EnableOutboxProcessing(Action<OutboxProcessingOptions>? optionsAction = null);

    /// <summary>
    /// Configures the database infrastructure to use the specified outbox store.
    /// </summary>
    /// <typeparam name="T">The type of outbox store to use.</typeparam>
    /// <returns>The database infrastructure configurator instance.</returns>
    IDatabaseInfrastructureConfigurator<TDbContext> UseOutboxStore<T>() where T : class, IOutboxStore;

    /// <summary>
    /// Registers a callback that runs once the configurator action has finished executing
    /// but before the DbContext is registered with the service collection. Use this hook
    /// from provider-specific extension methods (e.g. <c>UseNpgsql</c>) to defer work that
    /// depends on the final state of the configurator — most notably whether outbox
    /// processing has been enabled — regardless of the order in which the user chained calls.
    /// </summary>
    /// <param name="action">The callback to invoke. It receives the configurator in its final state.</param>
    /// <returns>The current configurator instance for chaining.</returns>
    IDatabaseInfrastructureConfigurator<TDbContext> OnConfigured(Action<IDatabaseInfrastructureConfigurator<TDbContext>> action);
}
