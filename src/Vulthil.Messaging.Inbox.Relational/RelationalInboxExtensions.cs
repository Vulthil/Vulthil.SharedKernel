using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vulthil.Messaging.Inbox.EntityFrameworkCore;

namespace Vulthil.Messaging.Inbox.Relational;

/// <summary>
/// Service-collection extensions that register the relational Entity Framework Core idempotency store.
/// </summary>
public static class RelationalInboxExtensions
{
    /// <summary>
    /// Registers an <see cref="IIdempotencyStore"/> backed by the application's <typeparamref name="TContext"/>,
    /// using ambient relational transactions. The context must be registered (e.g. via <c>AddDbContext</c>),
    /// implement <see cref="ISaveInboxMessages"/>, and map <see cref="InboxMessage"/> (via
    /// <see cref="RelationalInboxModelBuilderExtensions.ApplyRelationalInbox"/>).
    /// </summary>
    /// <typeparam name="TContext">The application's <see cref="DbContext"/> type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// An optional action to configure <see cref="InboxOptions"/>. Enable <see cref="InboxOptions.Retention"/> to
    /// register a background sweep that periodically prunes old idempotency markers.
    /// </param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddRelationalInbox<TContext>(
        this IServiceCollection services,
        Action<InboxOptions>? configure = null)
        where TContext : DbContext, ISaveInboxMessages
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IIdempotencyStore, RelationalIdempotencyStore<TContext>>();
        services.AddInboxRetention(configure);

        return services;
    }
}
