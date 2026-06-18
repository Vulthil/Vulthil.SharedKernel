namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// An <see cref="IOutboxStore"/> that can purge old terminal (processed or dead-lettered) rows for the retention
/// sweep. The EF Core outbox store implements this; the retention background service skips the sweep when the
/// registered store does not.
/// </summary>
public interface IOutboxRetentionStore
{
    /// <summary>
    /// Deletes up to <paramref name="batchSize"/> outbox rows that were processed or dead-lettered before
    /// <paramref name="olderThanUtc"/>. Pending rows are never deleted.
    /// </summary>
    /// <param name="olderThanUtc">The cutoff; only terminal rows whose <c>ProcessedOnUtc</c> or <c>FailedOnUtc</c> is older than this are deleted.</param>
    /// <param name="batchSize">The maximum number of rows to delete in this call.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The number of rows deleted.</returns>
    Task<int> DeleteProcessedAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken);
}
