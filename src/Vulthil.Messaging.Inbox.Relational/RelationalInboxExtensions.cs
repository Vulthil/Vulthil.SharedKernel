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
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddRelationalInbox<TContext>(this IServiceCollection services)
        where TContext : DbContext, ISaveInboxMessages
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IIdempotencyStore, RelationalIdempotencyStore<TContext>>();

        return services;
    }
}
