namespace Vulthil.Messaging.Abstractions.Consumers;

public interface IConsumer
{
    Task ConsumeAsync(IMessageContext messageContext, CancellationToken cancellationToken = default);
}

public interface IConsumer<in TMessage> : IConsumer
{
    Task IConsumer.ConsumeAsync(IMessageContext messageContext, CancellationToken cancellationToken)
    {
        if (messageContext is not IMessageContext<TMessage> typedMessageContext)
        {
            throw new ArgumentException($"Invalid message context type: {messageContext.GetType().Name}. Expected: {typeof(IMessageContext<TMessage>).Name}");
        }
        return ConsumeAsync(typedMessageContext, cancellationToken);
    }

    Task ConsumeAsync(IMessageContext<TMessage> messageContext, CancellationToken cancellationToken = default);
}


public interface IMessageContext
{
    string CorrelationId { get; }
    string RoutingKey { get; }
    IDictionary<string, object?> Headers { get; }
}
public interface IMessageContext<out TMessage> : IMessageContext
{
    TMessage Message { get; }
}
