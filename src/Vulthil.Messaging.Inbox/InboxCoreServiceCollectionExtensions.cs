using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Vulthil.Messaging.Inbox;

/// <summary>
/// Service-collection extension that registers the inbox infrastructure shared by every idempotency store.
/// </summary>
public static class InboxCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything an <see cref="IIdempotencyStore"/> needs beyond the store registration itself: the
    /// shared <see cref="TimeProvider"/>, the retention sweep (gated by <see cref="InboxRetentionOptions.Enabled"/>),
    /// and OpenTelemetry metrics (gated by <see cref="InboxOptions.EnableMetrics"/>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// An optional action to configure <see cref="InboxOptions"/>. Invoked once eagerly, against a single
    /// materialized <see cref="InboxOptions"/> instance, to evaluate both the retention and metrics gates, and once
    /// more when the options system materializes <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> for
    /// injected consumers.
    /// </param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <remarks>
    /// The first-party store extensions (<c>AddRelationalInbox</c>, <c>AddCosmosInbox</c>) register their
    /// <see cref="IIdempotencyStore"/> implementation and then call this method; a custom store package follows the
    /// same pattern — register its own <see cref="IIdempotencyStore"/> (typically with <c>TryAddScoped</c> so an
    /// application can still substitute a different implementation), then call <c>AddInboxCore</c> to get the same
    /// retention, metrics, and <see cref="TimeProvider"/> wiring the first-party stores get. There is no privileged
    /// first-party path: every store, first- or third-party, shares this one entry point.
    /// </remarks>
    public static IServiceCollection AddInboxCore(this IServiceCollection services, Action<InboxOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new InboxOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(TimeProvider.System);
        services.RegisterInboxRetention(configure, options);

        if (options.EnableMetrics)
        {
            services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddVulthilInboxInstrumentation());
        }

        return services;
    }
}
