using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Inbox;

/// <summary>
/// A persistence-agnostic store that records which messages have already been processed, enabling
/// exactly-once consumer processing on top of at-least-once delivery.
/// </summary>
/// <remarks>
/// Implementations own the transaction boundary: <see cref="BeginAsync"/> opens a unit of work that the
/// consumer's own writes enlist in, so that recording the idempotency marker and the consumer's business
/// changes commit atomically. The reference relational implementation lives in
/// <c>Vulthil.Messaging.Inbox.Relational</c>.
/// </remarks>
public interface IIdempotencyStore
{
    /// <summary>
    /// Begins an idempotent unit of work for the current delivery. The returned transaction must enclose the
    /// consumer invocation so the idempotency marker and the consumer's business writes commit together.
    /// </summary>
    /// <param name="context">The message context for the current delivery.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>An <see cref="IIdempotencyTransaction"/> that the caller commits on success or disposes to roll back.</returns>
    Task<IIdempotencyTransaction> BeginAsync(IMessageContext context, CancellationToken cancellationToken);
}
