namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// An <see cref="IOutboxStore"/> that can purge old terminal (processed or dead-lettered) rows for the retention
/// sweep. The EF Core outbox store implements this; the retention background service skips the sweep when the
/// registered store does not.
/// </summary>
public interface IOutboxRetentionStore
{
    /// <summary>
    /// Deletes up to <paramref name="batchSize"/> outbox rows that were processed before
    /// <paramref name="processedOlderThanUtc"/> or dead-lettered before <paramref name="deadLetteredOlderThanUtc"/>.
    /// Pending rows are never deleted.
    /// </summary>
    /// <param name="processedOlderThanUtc">The cutoff for processed rows; only a row whose <c>ProcessedOnUtc</c> is older than this is deleted.</param>
    /// <param name="deadLetteredOlderThanUtc">
    /// The cutoff for dead-lettered rows; only a row whose <c>FailedOnUtc</c> is older than this is deleted. Pass
    /// <see cref="DateTimeOffset.MinValue"/> to keep every dead-lettered row.
    /// </param>
    /// <param name="batchSize">The maximum number of rows to delete in this call.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The number of rows deleted.</returns>
    Task<int> DeleteProcessedAsync(DateTimeOffset processedOlderThanUtc, DateTimeOffset deadLetteredOlderThanUtc, int batchSize, CancellationToken cancellationToken);
}
