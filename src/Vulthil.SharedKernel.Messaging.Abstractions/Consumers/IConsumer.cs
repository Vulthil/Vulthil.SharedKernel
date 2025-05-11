namespace Vulthil.SharedKernel.Messaging.Abstractions.Consumers;

public interface IConsumer
{
    Task ConsumeAsync(object message, CancellationToken cancellationToken = default);
}

public interface IConsumer<in TMessage> : IConsumer
{
    Task IConsumer.ConsumeAsync(object message, CancellationToken cancellationToken)
    {
        if (message is not TMessage typedMessage)
        {
            throw new ArgumentException($"Invalid message type: {message.GetType().Name}. Expected: {typeof(TMessage).Name}");
        }
        return ConsumeAsync(typedMessage, cancellationToken);
    }

    Task ConsumeAsync(TMessage message, CancellationToken cancellationToken = default);
}


