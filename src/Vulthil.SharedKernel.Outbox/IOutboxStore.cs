namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Persistence-agnostic store the outbox engine relies on for both capture and relay. A relational or other
/// EF Core implementation lives in <c>Vulthil.SharedKernel.Outbox.EntityFrameworkCore</c> and its provider
/// packages; the engine itself takes no dependency on EF Core.
/// </summary>
/// <remarks>
/// The store owns the whole relay batch unit — provider locking, the transactional boundary, fetching the pending
/// rows, dispatching each, recording success/failure, and committing — so a relational implementation can run it
/// inside an EF Core execution strategy. The capture members are used by message-bridge publish filters to enlist
/// an outgoing message in the ambient business transaction.
/// </remarks>
public interface IOutboxStore
{
    /// <summary>
    /// Processes one batch of pending outbox messages: fetches up to the configured batch size, invokes
    /// <paramref name="dispatch"/> for each, records the outcome (success, or the returned error with a retry
    /// increment), and commits — all within the store's transactional boundary.
    /// </summary>
    /// <param name="dispatch">
    /// Delivers a single message; returns <see langword="null"/> on success, or an error description to record
    /// against the row as a failed attempt.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The number of messages fetched and processed in the batch.</returns>
    Task<int> ProcessBatchAsync(Func<OutboxMessageData, CancellationToken, Task<string?>> dispatch, CancellationToken cancellationToken);

    /// <summary>
    /// Stages an outbox message for persistence. Used by capture (e.g. a transactional bus-publish filter) to enlist
    /// the message in the ambient business transaction; call <see cref="SaveChangesAsync"/> to flush it.
    /// </summary>
    /// <param name="message">The message to stage.</param>
    void AddOutboxMessage(OutboxMessage message);

    /// <summary>
    /// Persists pending staged outbox messages.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The number of state entries written.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value indicating whether a database transaction is currently active. Capture uses this to decide
    /// whether an outgoing message is enlisted in the ambient transaction or published directly.
    /// </summary>
    bool IsInTransaction { get; }
}
