namespace Vulthil.Messaging.Abstractions.Consumers;

public interface IConsumer;
public interface IConsumer<in TMessage> : IConsumer
{
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
