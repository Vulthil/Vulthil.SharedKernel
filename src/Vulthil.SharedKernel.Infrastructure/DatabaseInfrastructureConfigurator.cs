using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

namespace Vulthil.SharedKernel.Infrastructure;

/// <summary>
/// Fluent configurator for database infrastructure including DbContext options and outbox processing.
/// </summary>
public sealed class DatabaseInfrastructureConfigurator : IDatabaseInfrastructureConfigurator
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
    /// <summary>
    /// Gets the outbox strategy type used by outbox processing.
    /// </summary>
    internal Type OutboxStrategyType { get; private set; } = typeof(BaseOutboxStrategy);

    /// <inheritdoc />
    public IHostApplicationBuilder HostApplicationBuilder { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseInfrastructureConfigurator"/> class.
    /// </summary>
    internal DatabaseInfrastructureConfigurator(IHostApplicationBuilder hostApplicationBuilder)
    {
        HostApplicationBuilder = hostApplicationBuilder;
    }


    /// <inheritdoc />
    public IDatabaseInfrastructureConfigurator EnableOutboxProcessing(Action<OutboxProcessingOptions>? optionsAction = null)
    {
        OutboxProcessingEnabled = true;
        OutboxOptionsAction = optionsAction ??= (o) => { };

        return this;
    }

    /// <inheritdoc/>
    public IDatabaseInfrastructureConfigurator UseOutboxStrategy<TStrategy>()
        where TStrategy : class, IOutboxStrategy
    {
        OutboxStrategyType = typeof(TStrategy);
        return this;
    }

    /// <inheritdoc/>
    public IDatabaseInfrastructureConfigurator ConfigureDbContextOptions(Action<DbContextOptionsBuilder> optionsBuilder)
    {
        var n = (IServiceProvider sp, DbContextOptionsBuilder options) => optionsBuilder(options);

        OptionsBuilder = n + ((sp, options) => AddInterceptors(options, sp));
        return this;
    }

    /// <inheritdoc/>
    public IDatabaseInfrastructureConfigurator ConfigureDbContextOptions(Action<IServiceProvider, DbContextOptionsBuilder> optionsBuilder)
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
