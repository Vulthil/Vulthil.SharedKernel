using Microsoft.Extensions.Options;
using Vulthil.Extensions.Retention;

namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// What the outbox retention sweep resolves on each sweep: it adapts the registered <see cref="IOutboxStore"/>, when it
/// implements <see cref="IOutboxRetentionStore"/>, to the sweep's delete operation. Processed rows use the sweep's own
/// cutoff (<see cref="OutboxRetentionOptions.RetentionPeriod"/>); dead-lettered rows use the cutoff of
/// <see cref="OutboxRetentionOptions.DeadLetterRetentionPeriod"/> when it is set, and the same cutoff otherwise.
/// </summary>
internal sealed class OutboxRetentionSweepTarget(IOutboxStore store, IOptions<OutboxProcessingOptions> options, TimeProvider timeProvider)
{
    /// <summary>
    /// Creates the delete operation for one sweep, or returns <see langword="null"/> when the registered store cannot
    /// delete old rows.
    /// </summary>
    internal RetentionSweepDeleter? CreateDeleter()
    {
        if (store is not IOutboxRetentionStore retentionStore)
        {
            return null;
        }

        if (options.Value.Retention.DeadLetterRetentionPeriod is not { } deadLetterRetentionPeriod)
        {
            return (olderThanUtc, batchSize, cancellationToken) =>
                retentionStore.DeleteProcessedAsync(olderThanUtc, olderThanUtc, batchSize, cancellationToken);
        }

        var deadLetteredOlderThanUtc = CutoffFor(deadLetterRetentionPeriod);
        return (olderThanUtc, batchSize, cancellationToken) =>
            retentionStore.DeleteProcessedAsync(olderThanUtc, deadLetteredOlderThanUtc, batchSize, cancellationToken);
    }

    private DateTimeOffset CutoffFor(TimeSpan retentionPeriod)
    {
        var now = timeProvider.GetUtcNow();

        // Subtracting a period that reaches back past DateTimeOffset.MinValue throws; no row is older than MinValue,
        // so such a period keeps every row.
        return retentionPeriod >= now - DateTimeOffset.MinValue ? DateTimeOffset.MinValue : now - retentionPeriod;
    }
}
