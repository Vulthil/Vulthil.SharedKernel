using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Outbox;

namespace Vulthil.SharedKernel.Infrastructure.Relational.OutboxProcessing;

/// <summary>
/// Base outbox strategy for relational database providers.
/// Uses EF Core relational APIs (<see cref="RelationalQueryableExtensions"/>)
/// for batch updates. Provider-specific strategies (e.g., Npgsql, SqlServer) should inherit
/// from this class and override <see cref="FetchMessagesAsync"/> to add row-level locking.
/// </summary>
public class RelationalOutboxStrategy : BaseOutboxStrategy
{

    /// <inheritdoc />
    public override async Task<List<OutboxMessageData>> FetchMessagesAsync(DbSet<OutboxMessage> outboxMessages, int batchSize, int maxRetries, CancellationToken cancellationToken)
    {
        return await outboxMessages
            .Where(o => o.ProcessedOnUtc == null && o.RetryCount < maxRetries)
            .OrderBy(o => o.OccurredOnUtc)
            .Take(batchSize)
            .Select(x => new OutboxMessageData(x.Id, x.Type, x.Content, x.TraceParent, x.TraceState, x.Destination, x.Metadata))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async Task UpdateMessagesAsync(ISaveOutboxMessages context, IReadOnlyList<Guid> successIds, IReadOnlyList<OutboxMessageFailure> failures, int maxRetries, DateTimeOffset processedOnUtc, CancellationToken cancellationToken)
    {
        if (successIds.Count > 0)
        {
            await context.OutboxMessages
                .Where(x => successIds.Contains(x.Id))
                .ExecuteUpdateAsync(
                    setter => setter.SetProperty(o => o.ProcessedOnUtc, processedOnUtc),
                    cancellationToken);
        }

        foreach (var failure in failures)
        {
            await context.OutboxMessages
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

            await context.OutboxMessages
                .Where(x => failedIds.Contains(x.Id) && x.RetryCount >= maxRetries)
                .ExecuteUpdateAsync(
                    setter => setter.SetProperty(o => o.ProcessedOnUtc, processedOnUtc),
                    cancellationToken);
        }
    }
}
