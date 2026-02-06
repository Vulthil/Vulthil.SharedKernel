namespace Vulthil.Messaging.Abstractions.Consumers;

public interface IRequestConsumer : IConsumer
{
    new Task<object> ConsumeAsync(IMessageContext messageContext, CancellationToken cancellationToken = default);
}

public interface IRequestConsumer<in TRequest, TResponse> : IRequestConsumer, IConsumer<TRequest>
    where TRequest : notnull
    where TResponse : notnull
{
    async Task<object> IRequestConsumer.ConsumeAsync(IMessageContext messageContext, CancellationToken cancellationToken)
    {
        if (messageContext is not IMessageContext<TRequest> typedMessageContext)
        {
            throw new ArgumentException($"Invalid message type: {messageContext.GetType().Name}. Expected: {typeof(IMessageContext<TRequest>).Name}");
        }
        return await ConsumeAsync(typedMessageContext, cancellationToken);
    }

    new Task<TResponse> ConsumeAsync(IMessageContext<TRequest> messageContext, CancellationToken cancellationToken = default);
}
