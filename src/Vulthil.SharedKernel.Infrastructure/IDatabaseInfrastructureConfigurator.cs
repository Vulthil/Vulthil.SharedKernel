using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

namespace Vulthil.SharedKernel.Infrastructure;

/// <summary>
/// Defines methods for configuring database infrastructure options for Entity Framework Core DbContext instances.
/// </summary>
public interface IDatabaseInfrastructureConfigurator
{
    /// <summary>
    /// Provides access to the host application builder for configuring services and settings.
    /// </summary>
    IHostApplicationBuilder HostApplicationBuilder { get; }

    /// <summary>
    /// Configures the DbContext options using the specified builder action.
    /// </summary>
    /// <param name="optionsBuilder">An action to configure the DbContextOptionsBuilder.</param>
    /// <returns>The current database infrastructure configurator instance.</returns>
    IDatabaseInfrastructureConfigurator ConfigureDbContextOptions(Action<DbContextOptionsBuilder> optionsBuilder);

    /// <summary>
    /// Configures the DbContext options using the specified delegate.
    /// </summary>
    /// <param name="optionsBuilder">A delegate to configure the DbContextOptionsBuilder with the service provider.</param>
    /// <returns>The current database infrastructure configurator instance.</returns>
    IDatabaseInfrastructureConfigurator ConfigureDbContextOptions(Action<IServiceProvider, DbContextOptionsBuilder> optionsBuilder);

    /// <summary>
    /// Enables outbox processing with optional configuration.
    /// </summary>
    /// <param name="optionsAction">An optional action to configure outbox processing options.</param>
    /// <returns>The current configurator instance for chaining.</returns>
    IDatabaseInfrastructureConfigurator EnableOutboxProcessing(Action<OutboxProcessingOptions>? optionsAction = null);

    /// <summary>
    /// Configures the database infrastructure to use the specified outbox strategy.
    /// </summary>
    /// <typeparam name="T">The type of outbox strategy to use.</typeparam>
    /// <returns>The database infrastructure configurator instance.</returns>
    IDatabaseInfrastructureConfigurator UseOutboxStrategy<T>() where T : class, IOutboxStrategy;
}
