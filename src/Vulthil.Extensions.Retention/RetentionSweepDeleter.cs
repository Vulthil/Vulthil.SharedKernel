namespace Vulthil.Extensions.Retention;

/// <summary>
/// Deletes up to <paramref name="batchSize"/> entries that became eligible for retention before
/// <paramref name="olderThanUtc"/> and returns how many were deleted. A store exposes its own delete operation in this
/// shape so the sweep registered by
/// <see cref="RetentionSweepServiceCollectionExtensions.AddRetentionSweep{TStore}"/> can drive it without knowing the
/// store's type.
/// </summary>
/// <param name="olderThanUtc">The cutoff; only entries older than this are deleted.</param>
/// <param name="batchSize">The maximum number of entries to delete in this call.</param>
/// <param name="cancellationToken">A token to observe for cancellation.</param>
/// <returns>The number of entries deleted.</returns>
public delegate Task<int> RetentionSweepDeleter(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken);
