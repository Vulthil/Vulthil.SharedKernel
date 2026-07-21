using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    /// <param name="configure">
    /// An optional action to configure <see cref="InboxOptions"/>. Invoked once eagerly (to evaluate
    /// <see cref="InboxRetentionOptions.Enabled"/>) and once more when the options system materializes
    /// <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> for injected consumers.
    /// </param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddInboxRetention(this IServiceCollection services, Action<InboxOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);

        var options = new InboxOptions();
        configure?.Invoke(options);

        return services.RegisterInboxRetention(configure, options);
    }

    /// <summary>
    /// Registers the options validation and the conditional hosted-service registration for the inbox retention
    /// sweep, using an already-materialized <paramref name="options"/> instance so a shared caller (e.g.
    /// <c>AddInboxCore</c>) does not need to invoke <paramref name="configure"/> a second time just to re-evaluate
    /// the <see cref="InboxRetentionOptions.Enabled"/> gate.
    /// </summary>
    internal static IServiceCollection RegisterInboxRetention(this IServiceCollection services, Action<InboxOptions>? configure, InboxOptions options)
    {
        services.AddOptions<InboxOptions>()
            .Configure(configure ?? (static _ => { }))
            .Validate(
                o => !o.Retention.Enabled
                    || o.Retention.RetentionPeriod > TimeSpan.Zero
                    && o.Retention.SweepInterval > TimeSpan.Zero
                    && o.Retention.BatchSize >= 1,
                "Inbox retention requires RetentionPeriod and SweepInterval greater than zero and BatchSize of at least 1 when enabled.")
            .ValidateOnStart();

        if (options.Retention.Enabled)
        {
            services.AddHostedService<InboxRetentionBackgroundService>();
        }

        return services;
    }
}
