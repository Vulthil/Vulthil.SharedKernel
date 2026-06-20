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
    /// <param name="configure">An optional action to configure <see cref="InboxOptions"/>.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddInboxRetention(this IServiceCollection services, Action<InboxOptions>? configure)
    {
        services.AddOptions<InboxOptions>()
            .Configure(configure ?? (static _ => { }))
            .Validate(
                o => !o.Retention.Enabled
                    || o.Retention.RetentionPeriod > TimeSpan.Zero
                    && o.Retention.SweepInterval > TimeSpan.Zero
                    && o.Retention.BatchSize >= 1,
                "Inbox retention requires RetentionPeriod and SweepInterval greater than zero and BatchSize of at least 1 when enabled.")
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var options = new InboxOptions();
        configure?.Invoke(options);
        if (options.Retention.Enabled)
        {
            services.AddHostedService<InboxRetentionBackgroundService>();
        }

        return services;
    }
}
