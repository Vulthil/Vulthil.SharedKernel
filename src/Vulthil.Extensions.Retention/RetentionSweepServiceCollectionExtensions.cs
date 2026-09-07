using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Vulthil.Extensions.Retention;

/// <summary>
/// Service-collection extension that registers a retention sweep for a store.
/// </summary>
public static class RetentionSweepServiceCollectionExtensions
{
    /// <summary>
    /// Registers a hosted service that sweeps the old entries of <typeparamref name="TStore"/>: on host start and then
    /// once per <see cref="RetentionSweepSettings.SweepInterval"/>, it resolves <typeparamref name="TStore"/> from a
    /// fresh scope, obtains its <see cref="RetentionSweepDeleter"/> through <paramref name="deleter"/>, and deletes
    /// entries older than <see cref="RetentionSweepSettings.RetentionPeriod"/> in batches of
    /// <see cref="RetentionSweepSettings.BatchSize"/> until fewer than a full batch remain. A store for which
    /// <paramref name="deleter"/> returns <see langword="null"/> is logged once as unsupported and never swept; every
    /// other sweep failure is logged and retried on the next interval. One sweep is registered per
    /// <typeparamref name="TStore"/>, so calling this again for the same store type is a no-op.
    /// </summary>
    /// <typeparam name="TStore">The registered store service the sweep resolves and deletes through.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The sweep's name, used in its log messages (for example <c>"Outbox"</c>).</param>
    /// <param name="settings">
    /// Resolves the sweep's settings once, when the hosted service is created — typically from the caller's own
    /// options, so configuration binding and post-configuration are honoured.
    /// </param>
    /// <param name="deleter">
    /// Adapts a resolved store to its delete operation, or returns <see langword="null"/> when the store cannot delete
    /// old entries.
    /// </param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddRetentionSweep<TStore>(
        this IServiceCollection services,
        string name,
        Func<IServiceProvider, RetentionSweepSettings> settings,
        Func<TStore, RetentionSweepDeleter?> deleter)
        where TStore : notnull
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(deleter);

        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService(provider => new RetentionSweepService<TStore>(
            name,
            settings(provider),
            deleter,
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<RetentionSweepService<TStore>>>()));

        return services;
    }
}
