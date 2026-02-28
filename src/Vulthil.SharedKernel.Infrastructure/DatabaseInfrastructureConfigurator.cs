using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

namespace Vulthil.SharedKernel.Infrastructure;

/// <summary>
/// Fluent configurator for database infrastructure including DbContext options and outbox processing.
/// </summary>
public sealed class DatabaseInfrastructureConfigurator
{
    /// <summary>
    /// Gets the configured <see cref="DbContextOptionsBuilder"/> delegate used when registering the DbContext,
    /// or <see langword="null"/> if not set.
    /// </summary>
    public Action<IServiceProvider, DbContextOptionsBuilder>? OptionsBuilder { get; private set; }
    /// <summary>
    /// Gets a value indicating whether outbox processing has been enabled.
    /// </summary>
    public bool OutboxProcessingEnabled { get; private set; }
    /// <summary>
    /// Gets the outbox processing options configuration action, or <see langword="null"/> if not set.
    /// </summary>
    public Action<OutboxProcessingOptions>? OutboxOptionsAction { get; private set; }
    internal Type OutboxStrategyType { get; private set; } = typeof(RelationalOutboxStrategy);
    internal DatabaseInfrastructureConfigurator() { }

    /// <summary>
    /// Enables outbox processing with optional configuration.
    /// </summary>
    /// <param name="optionsAction">An optional action to configure outbox processing options.</param>
    /// <returns>The current configurator instance for chaining.</returns>
    public DatabaseInfrastructureConfigurator EnableOutboxProcessing(Action<OutboxProcessingOptions>? optionsAction = null)
    {
        OutboxProcessingEnabled = true;
        OutboxOptionsAction = optionsAction ??= (o) => { };

        return this;
    }

    /// <summary>
    /// Specifies a custom outbox strategy implementation.
    /// </summary>
    /// <typeparam name="TStrategy">The outbox strategy type.</typeparam>
    /// <returns>The current configurator instance for chaining.</returns>
    public DatabaseInfrastructureConfigurator UseOutboxStrategy<TStrategy>()
        where TStrategy : class, IOutboxStrategy
    {
        OutboxStrategyType = typeof(TStrategy);
        return this;
    }

    /// <summary>
    /// Configures the DbContext options.
    /// </summary>
    /// <param name="optionsBuilder">An action to configure <see cref="DbContextOptionsBuilder"/>.</param>
    /// <returns>The current configurator instance for chaining.</returns>
    public DatabaseInfrastructureConfigurator ConfigureDbContextOptions(Action<DbContextOptionsBuilder> optionsBuilder)
    {
        var n = (IServiceProvider sp, DbContextOptionsBuilder options) => optionsBuilder(options);

        OptionsBuilder = n + ((sp, options) => AddInterceptors(options, sp));
        return this;
    }

    /// <summary>
    /// Configures the DbContext options with access to the service provider.
    /// </summary>
    /// <param name="optionsBuilder">An action to configure <see cref="DbContextOptionsBuilder"/> with service provider access.</param>
    /// <returns>The current configurator instance for chaining.</returns>
    public DatabaseInfrastructureConfigurator ConfigureDbContextOptions(Action<IServiceProvider, DbContextOptionsBuilder> optionsBuilder)
    {
        OptionsBuilder = optionsBuilder + ((sp, options) => AddInterceptors(options, sp));
        return this;
    }

    private void AddInterceptors(DbContextOptionsBuilder options, IServiceProvider sp)
    {
        if (OutboxProcessingEnabled)
        {
            var interceptor = sp.GetRequiredService<DomainEventsToOutboxMessageSaveChangesInterceptor>();
            options.AddInterceptors(interceptor);
        }
    }
}
