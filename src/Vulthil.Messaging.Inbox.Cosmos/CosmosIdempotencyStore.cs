using System.Net;
using Microsoft.Azure.Cosmos;
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
/// <remarks>
/// Deduplication uses a point read on the marker id (which is also the partition key), the cheapest and most
/// reliable existence check in Cosmos; a concurrent duplicate is detected by the resulting insert conflict.
/// </remarks>
/// <typeparam name="TContext">The application's Cosmos <see cref="DbContext"/> type, which must expose the inbox set.</typeparam>
internal sealed class CosmosIdempotencyStore<TContext>(TContext dbContext, TimeProvider timeProvider)
    : IIdempotencyStore
    where TContext : DbContext, ISaveInboxMessages
{
    public async Task<bool> ProcessAsync(string idempotencyKey, IMessageContext context, Func<CancellationToken, Task> process, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);
        ArgumentNullException.ThrowIfNull(process);

        if (await dbContext.InboxMessages.FindAsync([idempotencyKey], cancellationToken) is not null)
        {
            return false;
        }

        await process(cancellationToken);

        dbContext.InboxMessages.Add(new InboxMessage
        {
            MessageId = idempotencyKey,
            ProcessedOnUtc = timeProvider.GetUtcNow()
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsConflict(exception))
        {
            return false;
        }
    }

    private static bool IsConflict(DbUpdateException exception) =>
        exception.InnerException is CosmosException { StatusCode: HttpStatusCode.Conflict };
}
