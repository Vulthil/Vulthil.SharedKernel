using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Inbox;

/// <summary>
/// Resolves the idempotency key for a delivery of <typeparamref name="TMessage"/>. The key must be stable across
/// redeliveries (and ideally across producer republishes) of the same logical message.
/// </summary>
/// <typeparam name="TMessage">The message type the selector applies to.</typeparam>
public interface IInboxKeySelector<in TMessage>
    where TMessage : notnull
{
    /// <summary>
    /// Returns the idempotency key for the given delivery, or <see langword="null"/> to fall back to
    /// <see cref="IMessageContext.MessageId"/>.
    /// </summary>
    /// <param name="context">The message context for the current delivery.</param>
    /// <returns>The idempotency key, or <see langword="null"/> to use the message id.</returns>
    string? GetKey(IMessageContext<TMessage> context);
}
