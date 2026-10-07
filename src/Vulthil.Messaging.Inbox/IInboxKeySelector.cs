using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Inbox;

/// <summary>
/// Resolves the idempotency key for a delivery of <typeparamref name="TMessage"/>. The key must be stable across
/// redeliveries (and ideally across producer republishes) of the same logical message.
/// </summary>
/// <remarks>
/// <see cref="InboxConfiguratorExtensions.AddIdempotentInbox{TMessage}"/> registers one from its key selector. When
/// computing the key needs services from dependency injection, register your own implementation instead and call
/// <see cref="InboxConfiguratorExtensions.AddIdempotentInbox{TMessage}"/> without a key selector. A message type is
/// deduplicated on one key, so passing a key selector while your implementation is registered throws.
/// </remarks>
/// <typeparam name="TMessage">The message type the selector applies to.</typeparam>
public interface IInboxKeySelector<in TMessage>
    where TMessage : notnull
{
    /// <summary>
    /// Returns the idempotency key for the given delivery, or <see langword="null"/> or an empty string to fall
    /// back to <see cref="IMessageContext.MessageId"/>.
    /// </summary>
    /// <param name="context">The message context for the current delivery.</param>
    /// <returns>The idempotency key, or <see langword="null"/> or an empty string to use the message id.</returns>
    string? GetKey(IMessageContext<TMessage> context);
}
