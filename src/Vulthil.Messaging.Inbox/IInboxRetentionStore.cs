namespace Vulthil.Messaging.Inbox;

/// <summary>
/// An <see cref="IIdempotencyStore"/> that can purge old idempotency markers for the inbox retention sweep. The EF
/// Core idempotency stores implement this; the retention background service skips the sweep when the registered store
/// does not.
/// </summary>
public interface IInboxRetentionStore
{
    /// <summary>
    /// Deletes up to <paramref name="batchSize"/> idempotency markers that were processed before
    /// <paramref name="olderThanUtc"/>.
    /// </summary>
    /// <param name="olderThanUtc">The cutoff; only markers whose <c>ProcessedOnUtc</c> is older than this are deleted.</param>
    /// <param name="batchSize">The maximum number of markers to delete in this call.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The number of markers deleted.</returns>
    Task<int> DeleteProcessedAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken);
}
