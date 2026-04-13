using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Application.Data;

namespace Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

/// <summary>
/// Base outbox strategy for relational database providers.
/// Uses EF Core relational APIs (<see cref="RelationalQueryableExtensions"/>)
/// for batch updates. Provider-specific strategies (e.g., Npgsql, SqlServer) should inherit
/// from this class and override <see cref="FetchMessagesAsync"/> to add row-level locking.
/// </summary>
public class RelationalOutboxStrategy : IOutboxStrategy
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
            .Select(x => new OutboxMessageData(x.Id, x.Type, x.Content, x.TraceParent, x.TraceState))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task UpdateMessagesAsync(DbSet<OutboxMessage> outboxMessages, IReadOnlyList<Guid> successIds, IReadOnlyList<OutboxMessageFailure> failures, int maxRetries, DateTimeOffset processedOnUtc, CancellationToken cancellationToken)
    {
        if (successIds.Count > 0)
        {
            await outboxMessages
                .Where(x => successIds.Contains(x.Id))
                .ExecuteUpdateAsync(
                    setter => setter.SetProperty(o => o.ProcessedOnUtc, processedOnUtc),
                    cancellationToken);
        }

        foreach (var failure in failures)
        {
            await outboxMessages
                .Where(x => x.Id == failure.Id)
                .ExecuteUpdateAsync(
                    setter => setter
                        .SetProperty(o => o.RetryCount, o => o.RetryCount + 1)
                        .SetProperty(o => o.Error, failure.Error),
                    cancellationToken);
        }

        if (failures.Count > 0)
        {
            var failedIds = failures.Select(f => f.Id).ToList();

            await outboxMessages
                .Where(x => failedIds.Contains(x.Id) && x.RetryCount >= maxRetries)
                .ExecuteUpdateAsync(
                    setter => setter.SetProperty(o => o.ProcessedOnUtc, processedOnUtc),
                    cancellationToken);
        }
    }
}
