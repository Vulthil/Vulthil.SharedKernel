namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// The claim, step and record operations of one relay unit, bound to the transaction the store opened for it (see
/// <see cref="IOutboxStore.RunRelayUnitAsync{TResult}"/>).
/// </summary>
public interface IOutboxRelayUnit
{
    /// <summary>
    /// Claims up to <paramref name="batchSize"/> pending messages, oldest first: messages that are neither processed
    /// nor dead-lettered and whose retry count is below <paramref name="maxRetries"/>. A store with row locking claims
    /// rows that no concurrent relay unit can claim until this unit's transaction ends.
    /// </summary>
    /// <param name="batchSize">The maximum number of messages to claim.</param>
    /// <param name="maxRetries">Messages at or above this retry count are not claimed.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The claimed messages, oldest first.</returns>
    Task<IReadOnlyList<OutboxMessageData>> ClaimAsync(int batchSize, int maxRetries, CancellationToken cancellationToken);

    /// <summary>
    /// Runs the dispatch of one claimed message as a step of this unit, for a dispatch that shares the relay's scope and
    /// so the unit's transaction. When <paramref name="step"/> completes, the store saves the changes the step left
    /// pending, so they commit with the batch. When the step or that save throws, the store undoes the step's work
    /// without ending the unit's transaction — it rolls back to a savepoint where the provider supports one, and forgets
    /// the step's tracked changes — and then rethrows, so the unit can still record the outcomes of the batch.
    /// </summary>
    /// <param name="step">The dispatch to run; it receives the unit's cancellation token.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the step's changes are saved.</returns>
    Task RunStepAsync(Func<CancellationToken, Task> step, CancellationToken cancellationToken);

    /// <summary>
    /// Records the outcomes of claimed messages: each message in <paramref name="relayedIds"/> is marked processed, and
    /// each failure records its error and increments the message's retry count. A failure that brings the retry count
    /// to <paramref name="maxRetries"/> dead-letters the message.
    /// </summary>
    /// <param name="relayedIds">The identifiers of the messages that were delivered.</param>
    /// <param name="failures">The messages whose delivery failed, with the error to record.</param>
    /// <param name="maxRetries">A failed message whose retry count reaches this value is dead-lettered.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when the outcomes are recorded.</returns>
    Task RecordAsync(IReadOnlyList<Guid> relayedIds, IReadOnlyList<OutboxMessageFailure> failures, int maxRetries, CancellationToken cancellationToken);
}
