namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Persistence-agnostic store the outbox engine relies on for both capture and relay. A relational or other
/// EF Core implementation lives in <c>Vulthil.SharedKernel.Outbox.EntityFrameworkCore</c> and its provider
/// packages; the engine itself takes no dependency on EF Core.
/// </summary>
/// <remarks>
/// The store owns the relay's transactional boundary — provider locking, the transaction, claiming pending rows,
/// recording the outcomes, and committing — so a relational implementation can run it inside an EF Core execution
/// strategy. The relay cycle itself (which batch to claim, how to dispatch it, and when to run again) belongs to the
/// engine. The capture members are used by message-bridge publish filters to enlist an outgoing message in the ambient
/// business transaction.
/// </remarks>
public interface IOutboxStore
{
    /// <summary>
    /// Runs one relay unit inside the store's transactional boundary: opens the transaction (inside any retrying
    /// execution strategy the store uses), invokes <paramref name="unit"/> with the claim and record operations of that
    /// transaction, and commits when the unit returns.
    /// </summary>
    /// <remarks>
    /// A retrying execution strategy re-runs the whole unit after a transient failure, so <paramref name="unit"/> may be
    /// invoked more than once and must not carry state from one invocation to the next. Messages an abandoned run
    /// already dispatched are dispatched again, which the outbox's at-least-once delivery allows.
    /// </remarks>
    /// <typeparam name="TResult">The type of the unit's result.</typeparam>
    /// <param name="unit">The relay unit: claims a batch, dispatches it, and records the outcomes.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The result of the unit's committed run.</returns>
    Task<TResult> RunRelayUnitAsync<TResult>(Func<IOutboxRelayUnit, CancellationToken, Task<TResult>> unit, CancellationToken cancellationToken);

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
