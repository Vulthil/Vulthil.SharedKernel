namespace Vulthil.Messaging.Abstractions.Consumers;

public interface IRequestConsumer
{
    Task<object> ConsumeAsync(object message, CancellationToken cancellationToken = default);
}

public interface IRequestConsumer<TRequest, TResponse> : IRequestConsumer
    where TRequest : notnull
    where TResponse : notnull
{
    async Task<object> IRequestConsumer.ConsumeAsync(object message, CancellationToken cancellationToken)
    {
        if (message is not TRequest typedMessage)
        {
            throw new ArgumentException($"Invalid message type: {message.GetType().Name}. Expected: {typeof(TRequest).Name}");
        }
        return await ConsumeAsync(typedMessage, cancellationToken);
    }

    Task<TResponse> ConsumeAsync(TRequest message, CancellationToken cancellationToken = default);
}
