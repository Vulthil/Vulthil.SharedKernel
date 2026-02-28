using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Queues;

public class RequestConsumerConfigurator<TConsumer> : BaseConfigurator, IRequestConfigurator<TConsumer> where TConsumer : IRequestConsumer
{
    public IRequestConfigurator<TConsumer> Bind<TRequest, TResponse>(string routingKey)
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
