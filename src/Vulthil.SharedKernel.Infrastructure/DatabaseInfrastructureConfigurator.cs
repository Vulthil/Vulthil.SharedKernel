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
public sealed class DatabaseInfrastructureConfigurator<TDbContext> : IDatabaseInfrastructureConfigurator<TDbContext>
    where TDbContext : DbContext
{
    /// <inheritdoc />
    public bool OutboxProcessingEnabled { get; private set; }
    /// <summary>
    /// Gets the outbox processing options configuration action, or <see langword="null"/> if not set.
    /// </summary>
    public Action<OutboxProcessingOptions>? OutboxOptionsAction { get; private set; }
    /// <summary>
    /// Gets the outbox store type used by outbox processing. Defaults to the open generic
    /// <see cref="EntityFrameworkOutboxStore{TContext}"/> (closed over the context type at registration); a provider
    /// supplies a closed store type via <see cref="UseOutboxStore{TStore}"/>.
    /// </summary>
    internal Type OutboxStoreType { get; private set; } = typeof(EntityFrameworkOutboxStore<>);

    private readonly List<Action<IDatabaseInfrastructureConfigurator<TDbContext>>> _configuredCallbacks = [];

    /// <inheritdoc />
    public IHostApplicationBuilder HostApplicationBuilder { get; }

    /// <summary>
    /// Gets the <see cref="ServiceLifetime"/> that <typeparamref name="TDbContext"/> was registered
    /// with by the provider extension. Dependent services that wrap or delegate to the DbContext
    /// should adopt this lifetime so they cannot outlive (or be outlived by) the context they depend on.
    /// Falls back to <see cref="ServiceLifetime.Scoped"/> if the DbContext has not yet been registered.
    /// </summary>
    internal ServiceLifetime DbContextLifetime
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
    internal ServiceLifetime DbContextOptionsLifetime
        => DependencyInjection.FindLifetime(HostApplicationBuilder.Services, typeof(DbContextOptions<TDbContext>));

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseInfrastructureConfigurator{TDbContext}"/> class.
    /// </summary>
    internal DatabaseInfrastructureConfigurator(IHostApplicationBuilder hostApplicationBuilder)
    {
        HostApplicationBuilder = hostApplicationBuilder;
    }

    /// <inheritdoc />
    public IDatabaseInfrastructureConfigurator<TDbContext> EnableOutboxProcessing(Action<OutboxProcessingOptions>? optionsAction = null)
    {
        OutboxProcessingEnabled = true;
        OutboxOptionsAction = optionsAction ??= (o) => { };

        return this;
    }

    /// <inheritdoc/>
    public IDatabaseInfrastructureConfigurator<TDbContext> UseOutboxStore<TStore>()
        where TStore : class, IOutboxStore
    {
        OutboxStoreType = typeof(TStore);
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
    /// Finalises the configuration: runs every <see cref="OnConfigured"/> callback in registration order
    /// and, when outbox processing is enabled, registers an <see cref="IDbContextOptionsConfiguration{TContext}"/>
    /// that attaches the outbox <see cref="DomainEventsToOutboxMessageSaveChangesInterceptor"/> to the
    /// DbContext options. The configuration uses <see cref="DbContextOptionsLifetime"/> so it matches
    /// the lifetime EF Core has chosen for the options themselves.
    /// </summary>
    internal void FinalizeConfiguration()
    {
        foreach (var callback in _configuredCallbacks)
        {
            callback(this);
        }

        if (OutboxProcessingEnabled)
        {
            HostApplicationBuilder.Services.Add(new ServiceDescriptor(
                typeof(IDbContextOptionsConfiguration<TDbContext>),
                typeof(OutboxInterceptorDbContextOptionsConfiguration<TDbContext>),
                DbContextOptionsLifetime));
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
