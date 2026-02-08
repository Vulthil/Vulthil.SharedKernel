using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Queues;

public class RequestConsumerConfigurator<TConsumer> where TConsumer : IRequestConsumer
{
    internal Dictionary<MessageType, string> Overrides { get; } = [];

    public RequestConsumerConfigurator<TConsumer> BindRequest<TRequest, TResponse>(string routingKey)
        where TRequest : notnull
        where TResponse : notnull
    {
        if (!typeof(TConsumer).IsAssignableTo(typeof(IRequestConsumer<TRequest, TResponse>)))
        {
            throw new ArgumentException(
                $"Registration Error: '{typeof(TConsumer).Name}' cannot bind to request '{typeof(TRequest).Name}' " +
                $"because it does not implement IRequestConsumer<{typeof(TRequest).Name}, {typeof(TResponse).Name}>.");
        }

        Overrides[new(typeof(TRequest))] = routingKey;
        return this;
    }
}
