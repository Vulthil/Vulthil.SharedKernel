using Microsoft.EntityFrameworkCore;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Inbox.EntityFrameworkCore;

namespace Vulthil.Messaging.Inbox.Relational;

/// <summary>
/// An <see cref="IIdempotencyStore"/> backed by a relational Entity Framework Core provider. It runs the consumer
/// inside a database transaction so the consumer's own <c>SaveChanges</c> calls flush into it without committing;
/// the idempotency marker and the consumer's business writes then commit together, giving transactional
/// exactly-once processing.
/// </summary>
/// <remarks>
/// The transactional unit runs inside the context's <see cref="Microsoft.EntityFrameworkCore.Storage.IExecutionStrategy"/>
/// (<c>Database.CreateExecutionStrategy().ExecuteAsync</c>), so it works whether or not a retrying execution strategy
/// is configured — there is no need to disable EF Core retries. Under a retrying strategy a transient fault re-runs
/// the unit (including the consumer) on a cleared change tracker, which is consistent with at-least-once redelivery.
/// </remarks>
/// <typeparam name="TContext">The application's <see cref="DbContext"/> type, which must expose the inbox set.</typeparam>
internal sealed class RelationalIdempotencyStore<TContext>(TContext dbContext, TimeProvider timeProvider)
    : IIdempotencyStore, IInboxRetentionStore
    where TContext : DbContext, ISaveInboxMessages
{
    public async Task<int> DeleteProcessedAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken)
    {
        var keys = await dbContext.InboxMessages
            .Where(marker => marker.ProcessedOnUtc < olderThanUtc)
            .OrderBy(marker => marker.ProcessedOnUtc)
            .Take(batchSize)
            .Select(marker => marker.MessageId)
            .ToListAsync(cancellationToken);

        if (keys.Count == 0)
        {
            return 0;
        }

        return await dbContext.InboxMessages
            .Where(marker => keys.Contains(marker.MessageId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<bool> ProcessAsync(string idempotencyKey, IMessageContext context, Func<CancellationToken, Task> process, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);
        ArgumentNullException.ThrowIfNull(process);

        if (dbContext.Database.CurrentTransaction is not null)
        {
            return ProcessInAmbientTransactionAsync(idempotencyKey, process, cancellationToken);
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(
            async token => await ProcessInOwnTransactionAsync(idempotencyKey, process, token),
            cancellationToken);
    }

    private async Task<bool> ProcessInOwnTransactionAsync(string idempotencyKey, Func<CancellationToken, Task> process, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (await HasProcessedAsync(idempotencyKey, cancellationToken))
        {
            return false;
        }

        await process(cancellationToken);
        AddMarker(idempotencyKey);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);

            if (await HasProcessedAsync(idempotencyKey, cancellationToken))
            {
                return false;
            }

            throw;
        }
    }

    private async Task<bool> ProcessInAmbientTransactionAsync(string idempotencyKey, Func<CancellationToken, Task> process, CancellationToken cancellationToken)
    {
        if (await HasProcessedAsync(idempotencyKey, cancellationToken))
        {
            return false;
        }

        await process(cancellationToken);
        AddMarker(idempotencyKey);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            if (await HasProcessedAsync(idempotencyKey, cancellationToken))
            {
                return false;
            }

            throw;
        }
    }

    private Task<bool> HasProcessedAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        dbContext.InboxMessages.AsNoTracking().AnyAsync(x => x.MessageId == idempotencyKey, cancellationToken);

    private void AddMarker(string idempotencyKey) =>
        dbContext.InboxMessages.Add(new InboxMessage
        {
            MessageId = idempotencyKey,
            ProcessedOnUtc = timeProvider.GetUtcNow()
        });
}
