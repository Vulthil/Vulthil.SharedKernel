using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

namespace Vulthil.SharedKernel.Infrastructure;

/// <summary>
/// Fluent configurator for database infrastructure including outbox processing. The DbContext itself
/// is registered by provider-specific extensions (e.g. <c>UseNpgsql</c>) via the <see cref="OnConfigured"/>
/// hook, not by this class.
/// </summary>
internal sealed class DatabaseInfrastructureConfigurator<TDbContext> : IDatabaseInfrastructureConfigurator<TDbContext>
    where TDbContext : DbContext
{
    private readonly List<Action<IDatabaseInfrastructureConfigurator<TDbContext>>> _configuredCallbacks = [];
    private Type? _selectedOutboxStoreType;
    private Type? _defaultOutboxStoreType;

    /// <inheritdoc />
    public bool OutboxProcessingEnabled { get; private set; }

    /// <summary>
    /// Gets the outbox processing options configuration action, or <see langword="null"/> when none was given.
    /// </summary>
    public Action<OutboxProcessingOptions>? OutboxOptionsAction { get; private set; }

    /// <summary>
    /// Gets the outbox store type used by outbox processing: the application's choice from
    /// <see cref="UseOutboxStore{TStore}"/>, else a provider's from <see cref="UseDefaultOutboxStore{TStore}"/>, else
    /// <see langword="null"/> for <see cref="EntityFrameworkOutboxStore{TContext}"/>.
    /// </summary>
    public Type? OutboxStoreType => _selectedOutboxStoreType ?? _defaultOutboxStoreType;

    /// <inheritdoc />
    public IHostApplicationBuilder HostApplicationBuilder { get; }

    /// <summary>
    /// Gets the <see cref="ServiceLifetime"/> that <typeparamref name="TDbContext"/> was registered
    /// with by the provider extension. Dependent services that wrap or delegate to the DbContext
    /// should adopt this lifetime so they cannot outlive (or be outlived by) the context they depend on.
    /// Falls back to <see cref="ServiceLifetime.Scoped"/> if the DbContext has not yet been registered.
    /// </summary>
    public ServiceLifetime DbContextLifetime
        => DependencyInjection.FindLifetime(HostApplicationBuilder.Services, typeof(TDbContext));

    /// <summary>
    /// Gets the <see cref="ServiceLifetime"/> that <see cref="DbContextOptions{TContext}"/> for
    /// <typeparamref name="TDbContext"/> was registered with. EF Core resolves
    /// <see cref="IDbContextOptionsConfiguration{TContext}"/> from the same scope as the options, so
    /// any options-configuration we register must match this lifetime to avoid scope-validation
    /// errors (a scoped configuration cannot be resolved when the options themselves are built from
    /// the root provider, and vice versa). Falls back to <see cref="ServiceLifetime.Scoped"/> if the
    /// options have not yet been registered.
    /// </summary>
    public ServiceLifetime DbContextOptionsLifetime
        => DependencyInjection.FindLifetime(HostApplicationBuilder.Services, typeof(DbContextOptions<TDbContext>));

    public DatabaseInfrastructureConfigurator(IHostApplicationBuilder hostApplicationBuilder)
    {
        HostApplicationBuilder = hostApplicationBuilder;
    }

    /// <inheritdoc />
    public IDatabaseInfrastructureConfigurator<TDbContext> EnableOutboxProcessing(Action<OutboxProcessingOptions>? optionsAction = null)
    {
        OutboxProcessingEnabled = true;
        OutboxOptionsAction = optionsAction;

        return this;
    }

    /// <inheritdoc/>
    public IDatabaseInfrastructureConfigurator<TDbContext> UseOutboxStore<TStore>()
        where TStore : class, IOutboxStore
    {
        _selectedOutboxStoreType = typeof(TStore);
        return this;
    }

    /// <inheritdoc/>
    public IDatabaseInfrastructureConfigurator<TDbContext> UseDefaultOutboxStore<TStore>()
        where TStore : class, IOutboxStore
    {
        _defaultOutboxStoreType = typeof(TStore);
        return this;
    }

    /// <inheritdoc />
    public IDatabaseInfrastructureConfigurator<TDbContext> OnConfigured(Action<IDatabaseInfrastructureConfigurator<TDbContext>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _configuredCallbacks.Add(action);
        return this;
    }

    /// <summary>
    /// Finalises the configuration: runs every <see cref="OnConfigured"/> callback in registration order, so the
    /// provider has registered the DbContext before the lifetimes are read.
    /// </summary>
    public void FinalizeConfiguration()
    {
        foreach (var callback in _configuredCallbacks)
        {
            callback(this);
        }
    }
}

internal sealed class OutboxInterceptorDbContextOptionsConfiguration<TContext> : IDbContextOptionsConfiguration<TContext>
    where TContext : DbContext
{
    public void Configure(IServiceProvider serviceProvider, DbContextOptionsBuilder optionsBuilder)
    {
        // Attaches every registered outbox interceptor (the domain-event capture interceptor, plus a relational
        // transaction-commit interceptor when a relational provider contributes one).
        optionsBuilder.AddInterceptors(serviceProvider.GetServices<IOutboxInterceptor>());
    }
}
