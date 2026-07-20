using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Inbox;

/// <summary>
/// A persistence-agnostic store that records which messages have already been processed, enabling
/// exactly-once consumer processing on a transactional store (effectively-once otherwise) on top of
/// at-least-once delivery.
/// </summary>
/// <remarks>
/// The store owns the whole idempotent unit of work: it decides whether the delivery is a duplicate, runs the
/// consumer, and records the idempotency marker — atomically with the consumer's business writes on a relational
/// provider, or best-effort on a store without cross-partition transactions. Owning the unit lets a relational
/// implementation run it inside an EF Core execution strategy, so it works whether or not a retrying execution
/// strategy is configured. The reference relational implementation lives in <c>Vulthil.Messaging.Inbox.Relational</c>.
/// </remarks>
public interface IIdempotencyStore
{
    /// <summary>
    /// Processes the current delivery exactly once for the given idempotency key. If the key has not been seen,
    /// <paramref name="process"/> is invoked and the idempotency marker is recorded — atomically with the consumer's
    /// business writes on a relational provider. If the key has already been recorded, <paramref name="process"/> is
    /// skipped.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key for the current delivery.</param>
    /// <param name="context">
    /// The message context for the current delivery — optional input for implementations (e.g. to read metadata
    /// or headers); the built-in stores do not require it.
    /// </param>
    /// <param name="process">
    /// The consumer invocation to run when the delivery is not a duplicate. The token an implementation passes to
    /// this callback does not flow into the consumer: the consumer observes the delivery's own
    /// <see cref="IMessageContext.CancellationToken"/>, so the callback token cannot cancel the consumer body.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="process"/> ran and the marker was recorded; <see langword="false"/>
    /// if the delivery was a duplicate and was skipped.
    /// </returns>
    Task<bool> ProcessAsync(string idempotencyKey, IMessageContext context, Func<CancellationToken, Task> process, CancellationToken cancellationToken);
}
