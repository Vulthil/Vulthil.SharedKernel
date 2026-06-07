using Microsoft.EntityFrameworkCore;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Inbox.EntityFrameworkCore;

namespace Vulthil.Messaging.Inbox.Cosmos;

/// <summary>
/// An <see cref="IIdempotencyStore"/> backed by the Azure Cosmos DB EF Core provider. Cosmos has no
/// cross-partition transaction, so it cannot enlist the consumer's writes and the idempotency marker in one
/// atomic unit. This store therefore provides <b>effectively-once</b> processing: best-effort deduplication that
/// relies on the consumer's business writes being idempotent for the (rare) interleavings the marker cannot guard.
/// </summary>
/// <typeparam name="TContext">The application's Cosmos <see cref="DbContext"/> type, which must expose the inbox set.</typeparam>
internal sealed class CosmosIdempotencyStore<TContext>(TContext dbContext, TimeProvider timeProvider)
    : IIdempotencyStore
    where TContext : DbContext, ISaveInboxMessages
{
    public Task<IIdempotencyTransaction> BeginAsync(IMessageContext context, CancellationToken cancellationToken) =>
        Task.FromResult<IIdempotencyTransaction>(new CosmosIdempotencyTransaction<TContext>(dbContext, timeProvider));
}
