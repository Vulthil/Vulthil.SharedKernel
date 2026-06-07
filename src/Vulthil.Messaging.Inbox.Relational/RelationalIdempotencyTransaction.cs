using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Vulthil.Messaging.Inbox.Relational;

/// <summary>
/// The unit of work created by <see cref="RelationalIdempotencyStore{TContext}"/> for a single delivery.
/// </summary>
/// <typeparam name="TContext">The application's <see cref="DbContext"/> type.</typeparam>
internal sealed class RelationalIdempotencyTransaction<TContext>(
    TContext dbContext,
    IDbContextTransaction transaction,
    bool ownsTransaction,
    TimeProvider timeProvider) : IIdempotencyTransaction
    where TContext : DbContext, ISaveInboxMessages
{
    private bool _transactionDisposed;

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

            if (ownsTransaction)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException) when (ownsTransaction)
        {
            await DisposeTransactionAsync(rollback: true, cancellationToken);

            var alreadyProcessed = await dbContext.InboxMessages
                .AsNoTracking()
                .AnyAsync(x => x.MessageId == messageId, cancellationToken);

            if (!alreadyProcessed)
            {
                throw;
            }
        }
    }

    public ValueTask DisposeAsync() => DisposeTransactionAsync(rollback: false, CancellationToken.None);

    private async ValueTask DisposeTransactionAsync(bool rollback, CancellationToken cancellationToken)
    {
        if (!ownsTransaction || _transactionDisposed)
        {
            return;
        }

        _transactionDisposed = true;

        if (rollback)
        {
            await transaction.RollbackAsync(cancellationToken);
        }

        await transaction.DisposeAsync();
    }
}
