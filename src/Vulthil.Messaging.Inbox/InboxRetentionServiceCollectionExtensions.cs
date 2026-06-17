using Microsoft.Extensions.DependencyInjection;

namespace Vulthil.Messaging.Inbox;

/// <summary>
/// Service-collection extension that enables the inbox retention sweep.
/// </summary>
public static class InboxRetentionServiceCollectionExtensions
{
    /// <summary>
    /// Registers the inbox retention background service, which periodically deletes idempotency markers older than
    /// <see cref="InboxRetentionOptions.RetentionPeriod"/>. Calling this is what enables the sweep; it runs when the
    /// registered <see cref="IIdempotencyStore"/> implements <see cref="IInboxRetentionStore"/> (the EF Core stores
    /// do).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">An optional action to configure <see cref="InboxRetentionOptions"/>.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddInboxRetention(
        this IServiceCollection services,
        Action<InboxRetentionOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<InboxRetentionOptions>()
            .Configure(configureOptions ?? (static _ => { }))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHostedService<InboxRetentionBackgroundService>();

        return services;
    }
}
