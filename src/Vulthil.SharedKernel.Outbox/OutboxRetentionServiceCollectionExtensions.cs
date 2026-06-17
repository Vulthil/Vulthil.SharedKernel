using Microsoft.Extensions.DependencyInjection;

namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Service-collection extension that enables the outbox retention sweep.
/// </summary>
public static class OutboxRetentionServiceCollectionExtensions
{
    /// <summary>
    /// Registers the outbox retention background service, which periodically deletes processed and dead-lettered
    /// outbox rows older than <see cref="OutboxRetentionOptions.RetentionPeriod"/>. Calling this is what enables the
    /// sweep; it runs when the registered <see cref="IOutboxStore"/> implements <see cref="IOutboxRetentionStore"/>
    /// (the EF Core store does).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">An optional action to configure <see cref="OutboxRetentionOptions"/>.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddOutboxRetention(
        this IServiceCollection services,
        Action<OutboxRetentionOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<OutboxRetentionOptions>()
            .Configure(configureOptions ?? (static _ => { }))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHostedService<OutboxRetentionBackgroundService>();

        return services;
    }
}
