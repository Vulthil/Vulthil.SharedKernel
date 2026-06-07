using Microsoft.EntityFrameworkCore;
using Vulthil.Messaging.Inbox.EntityFrameworkCore;

namespace Vulthil.Messaging.Inbox.Cosmos;

/// <summary>
/// The idempotent unit created by <see cref="CosmosIdempotencyStore{TContext}"/> for a single delivery. There is
/// no ambient transaction (Cosmos does not support one across documents); the marker is written as its own
/// document after the consumer has committed its work.
/// </summary>
/// <typeparam name="TContext">The application's Cosmos <see cref="DbContext"/> type.</typeparam>
internal sealed class CosmosIdempotencyTransaction<TContext>(TContext dbContext, TimeProvider timeProvider)
    : IIdempotencyTransaction
    where TContext : DbContext, ISaveInboxMessages
{
    public Task<bool> HasProcessedAsync(string messageId, CancellationToken cancellationToken) =>
        dbContext.InboxMessages.AsNoTracking().AnyAsync(x => x.MessageId == messageId, cancellationToken);

    public async Task CommitAsync(string messageId, CancellationToken cancellationToken)
    {
        dbContext.InboxMessages.Add(new InboxMessage
        {
            MessageId = messageId,
            ProcessedOnUtc = timeProvider.GetUtcNow()
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var alreadyProcessed = await dbContext.InboxMessages
                .AsNoTracking()
                .AnyAsync(x => x.MessageId == messageId, cancellationToken);

            if (!alreadyProcessed)
            {
                throw;
            }
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
