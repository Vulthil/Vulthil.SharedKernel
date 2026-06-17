using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

namespace Vulthil.SharedKernel.Infrastructure.Relational.OutboxProcessing;

/// <summary>
/// Outbox store for relational database providers. Records processed and failed messages with set-based
/// <c>ExecuteUpdate</c> calls instead of materializing rows. Provider packages (e.g. Npgsql, MySQL) inherit from this
/// class and override <see cref="EntityFrameworkOutboxStore{TContext}.FetchMessagesAsync"/> to add row-level locking.
/// </summary>
/// <typeparam name="TContext">The application's <see cref="DbContext"/>, which exposes the outbox set.</typeparam>
public class RelationalOutboxStore<TContext>(TContext dbContext, TimeProvider timeProvider, IOptions<OutboxProcessingOptions> options)
    : EntityFrameworkOutboxStore<TContext>(dbContext, timeProvider, options)
    where TContext : DbContext, ISaveOutboxMessages
{
    /// <inheritdoc />
    protected override async Task UpdateMessagesAsync(IReadOnlyList<Guid> successIds, IReadOnlyList<OutboxMessageFailure> failures, int maxRetries, DateTimeOffset processedOnUtc, CancellationToken cancellationToken)
    {
        if (successIds.Count > 0)
        {
            await OutboxMessages
                .Where(x => successIds.Contains(x.Id))
                .ExecuteUpdateAsync(
                    setter => setter.SetProperty(o => o.ProcessedOnUtc, processedOnUtc),
                    cancellationToken);
        }

        foreach (var failure in failures)
        {
            await OutboxMessages
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

            var deadLettered = await OutboxMessages
                .Where(x => failedIds.Contains(x.Id) && x.RetryCount >= maxRetries)
                .Select(x => new { x.Id, x.RetryCount, x.Error })
                .ToListAsync(cancellationToken);

            if (deadLettered.Count > 0)
            {
                await OutboxMessages
                    .Where(x => failedIds.Contains(x.Id) && x.RetryCount >= maxRetries)
                    .ExecuteUpdateAsync(
                        setter => setter.SetProperty(o => o.FailedOnUtc, processedOnUtc),
                        cancellationToken);

                foreach (var item in deadLettered)
                {
                    Logger.LogError("Outbox message {OutboxMessageId} dead-lettered after {RetryCount} failed attempts: {OutboxError}", item.Id, item.RetryCount, item.Error);
                }
            }
        }
    }

    /// <inheritdoc />
    public override Task<int> DeleteProcessedAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken) =>
        OutboxMessages
            .Where(o => o.ProcessedOnUtc != null && o.ProcessedOnUtc < olderThanUtc
                || o.FailedOnUtc != null && o.FailedOnUtc < olderThanUtc)
            .ExecuteDeleteAsync(cancellationToken);
}

