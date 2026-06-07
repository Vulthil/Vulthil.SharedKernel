namespace Vulthil.Messaging.Inbox;

/// <summary>
/// An idempotent unit of work for a single delivery, created by <see cref="IIdempotencyStore.BeginAsync"/>.
/// </summary>
/// <remarks>
/// Disposing the transaction without calling <see cref="CommitAsync"/> rolls back both the idempotency marker
/// and any business writes the consumer performed, so a failed delivery is reprocessed cleanly on redelivery.
/// </remarks>
public interface IIdempotencyTransaction : IAsyncDisposable
{
    /// <summary>
    /// Determines whether a message with the given idempotency key has already been processed.
    /// </summary>
    /// <param name="messageId">The idempotency key for the current delivery.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns><see langword="true"/> if the message was already processed and should be skipped; otherwise <see langword="false"/>.</returns>
    Task<bool> HasProcessedAsync(string messageId, CancellationToken cancellationToken);

    /// <summary>
    /// Records the message as processed and commits the unit of work, persisting the idempotency marker and the
    /// consumer's business writes atomically.
    /// </summary>
    /// <remarks>
    /// Implementations treat a uniqueness conflict on the key as a concurrent duplicate (the message is considered
    /// already processed) rather than surfacing an error.
    /// </remarks>
    /// <param name="messageId">The idempotency key for the current delivery.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task CommitAsync(string messageId, CancellationToken cancellationToken);
}
