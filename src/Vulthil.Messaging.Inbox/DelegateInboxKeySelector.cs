using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Inbox;

/// <summary>
/// An <see cref="IInboxKeySelector{TMessage}"/> backed by a delegate. A <see langword="null"/> delegate yields the
/// default behaviour of falling back to <see cref="IMessageContext.MessageId"/>.
/// </summary>
/// <typeparam name="TMessage">The message type the selector applies to.</typeparam>
internal sealed class DelegateInboxKeySelector<TMessage>(Func<IMessageContext<TMessage>, string?>? keySelector)
    : IInboxKeySelector<TMessage>
    where TMessage : notnull
{
    /// <summary>
    /// Gets the delegate this selector was registered with, or <see langword="null"/> for the message-id default.
    /// </summary>
    public Func<IMessageContext<TMessage>, string?>? KeySelector { get; } = keySelector;

    public string? GetKey(IMessageContext<TMessage> context) => KeySelector?.Invoke(context);
}
