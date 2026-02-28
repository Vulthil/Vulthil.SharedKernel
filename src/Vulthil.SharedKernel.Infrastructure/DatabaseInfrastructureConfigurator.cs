using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

namespace Vulthil.SharedKernel.Infrastructure;

public sealed class DatabaseInfrastructureConfigurator
{
    public Action<IServiceProvider, DbContextOptionsBuilder>? OptionsBuilder { get; private set; }
    public bool OutboxProcessingEnabled { get; private set; }
    public Action<OutboxProcessingOptions>? OutboxOptionsAction { get; private set; }
    internal Type OutboxStrategyType { get; private set; } = typeof(RelationalOutboxStrategy);
    internal DatabaseInfrastructureConfigurator() { }

    public DatabaseInfrastructureConfigurator EnableOutboxProcessing(Action<OutboxProcessingOptions>? optionsAction = null)
    {
        OutboxProcessingEnabled = true;
        OutboxOptionsAction = optionsAction ??= (o) => { };

        return this;
    }

    public DatabaseInfrastructureConfigurator UseOutboxStrategy<TStrategy>()
        where TStrategy : class, IOutboxStrategy
    {
        OutboxStrategyType = typeof(TStrategy);
        return this;
    }

    public DatabaseInfrastructureConfigurator ConfigureDbContextOptions(Action<DbContextOptionsBuilder> optionsBuilder)
    {
        var n = (IServiceProvider sp, DbContextOptionsBuilder options) => optionsBuilder(options);

        OptionsBuilder = n + ((sp, options) => AddInterceptors(options, sp));
        return this;
    }

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
