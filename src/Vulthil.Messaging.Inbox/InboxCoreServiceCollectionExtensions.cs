using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Vulthil.Messaging.Inbox;

/// <summary>
/// Registration shared by every inbox store package (<c>AddRelationalInbox</c>, <c>AddCosmosInbox</c>). The store
/// extensions differ only in which <see cref="IIdempotencyStore"/> implementation they register; the retention
/// sweep, OpenTelemetry metrics, and <see cref="TimeProvider"/> registration are identical regardless of the store,
/// so they live here once instead of being duplicated per store package. Internal, and visible to the store
/// packages via <c>InternalsVisibleTo</c>.
/// </summary>
internal static class InboxCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything an <c>AddXyzInbox</c> store extension needs beyond its own store type: the shared
    /// <see cref="TimeProvider"/>, the retention sweep (gated by <see cref="InboxRetentionOptions.Enabled"/>), and
    /// OpenTelemetry metrics (gated by <see cref="InboxOptions.EnableMetrics"/>). <paramref name="configure"/> is
    /// invoked exactly once here to evaluate both gates from a single materialized <see cref="InboxOptions"/>
    /// instance, plus once more when the options system resolves <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/>
    /// for injected consumers.
    /// </summary>
    internal static IServiceCollection AddInboxCore(this IServiceCollection services, Action<InboxOptions>? configure)
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
