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
    public string? GetKey(IMessageContext<TMessage> context) => keySelector?.Invoke(context);
}
