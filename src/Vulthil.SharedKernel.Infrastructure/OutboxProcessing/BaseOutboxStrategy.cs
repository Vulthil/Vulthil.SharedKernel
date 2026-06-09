using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Application.Data;

namespace Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

/// <summary>
/// Provides a base implementation for outbox processing strategies, defining common operations for transactional
/// message handling in an outbox pattern.
/// </summary>
/// <remarks>This abstract class supplies default behaviors for starting transactions, fetching messages, and
/// updating message states in an outbox. Derived classes can override these methods to implement custom logic for
/// message retrieval, processing, and error handling. The class is intended to be used as a foundation for building
/// outbox strategies that coordinate reliable message delivery in distributed systems.</remarks>
public class BaseOutboxStrategy : IOutboxStrategy
{
    /// <inheritdoc />
    public virtual async Task<IDbTransaction?> BeginTransactionAsync(ISaveOutboxMessages context, CancellationToken cancellationToken)
    {
        if (context is IUnitOfWork unitOfWork)
        {
            return await unitOfWork.BeginTransactionAsync(cancellationToken);
        }

        return null;
    }

    /// <inheritdoc />
    public virtual async Task<List<OutboxMessageData>> FetchMessagesAsync(DbSet<OutboxMessage> outboxMessages, int batchSize, int maxRetries, CancellationToken cancellationToken)
    {
        return await outboxMessages
            .Where(o => o.ProcessedOnUtc == null && o.RetryCount < maxRetries)
            .OrderBy(o => o.OccurredOnUtc)
            .Take(batchSize)
            .Select(x => new OutboxMessageData(x.Id, x.Type, x.Content, x.TraceParent, x.TraceState, x.Destination, x.Metadata))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task UpdateMessagesAsync(ISaveOutboxMessages context, IReadOnlyList<Guid> successIds, IReadOnlyList<OutboxMessageFailure> failures, int maxRetries, DateTimeOffset processedOnUtc, CancellationToken cancellationToken)
    {
        if (successIds.Count > 0)
        {
            var messages = await context.OutboxMessages
                .Where(x => successIds.Contains(x.Id))
                .ToArrayAsync(cancellationToken);

            foreach (var item in messages)
            {
                item.ProcessedOnUtc = processedOnUtc;
            }
            await context.SaveChangesAsync(cancellationToken);
        }

        foreach (var failure in failures)
        {
            var messages = await context.OutboxMessages
                .Where(x => x.Id == failure.Id)
                .ToArrayAsync(cancellationToken);

            foreach (var item in messages)
            {
                item.RetryCount++;
                item.Error = failure.Error;
            }
            await context.SaveChangesAsync(cancellationToken);
        }

        if (failures.Count > 0)
        {
            var failedIds = failures.Select(f => f.Id).ToList();

            var messages = await context.OutboxMessages
                .Where(x => failedIds.Contains(x.Id) && x.RetryCount >= maxRetries)
                .ToArrayAsync(cancellationToken);

            foreach (var item in messages)
            {
                item.ProcessedOnUtc = processedOnUtc;
            }
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
