using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulthil.Messaging.Inbox.EntityFrameworkCore;

namespace Vulthil.Messaging.Inbox.Cosmos;

/// <summary>
/// Service-collection extensions that register the Cosmos idempotency store.
/// </summary>
public static class CosmosInboxExtensions
{
    /// <summary>
    /// Registers an <see cref="IIdempotencyStore"/> backed by the application's Cosmos <typeparamref name="TContext"/>.
    /// The context must be registered, implement <see cref="ISaveInboxMessages"/>, and map <see cref="InboxMessage"/>
    /// (via <see cref="CosmosInboxModelBuilderExtensions.ApplyCosmosInbox"/>). Provides effectively-once processing — pair it with
    /// idempotent consumer writes.
    /// </summary>
    /// <typeparam name="TContext">The application's Cosmos <see cref="DbContext"/> type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// An optional action to configure <see cref="InboxOptions"/>. Enable <see cref="InboxOptions.Retention"/> to
    /// register a background sweep that periodically prunes old idempotency markers.
    /// </param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddCosmosInbox<TContext>(
        this IServiceCollection services,
        Action<InboxOptions>? configure = null)
        where TContext : DbContext, ISaveInboxMessages
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IIdempotencyStore, CosmosIdempotencyStore<TContext>>();
        services.AddInboxRetention(configure);

        var options = new InboxOptions();
        configure?.Invoke(options);
        if (options.EnableMetrics)
        {
            services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddVulthilInboxInstrumentation());
        }

        return services;
    }
}
