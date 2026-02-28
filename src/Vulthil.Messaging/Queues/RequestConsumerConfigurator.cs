using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Queues;

/// <summary>
/// Provides per-consumer configuration for request/reply routing key overrides and retry policies.
/// </summary>
/// <typeparam name="TConsumer">The request consumer type being configured.</typeparam>
public class RequestConsumerConfigurator<TConsumer> : BaseConfigurator, IRequestConfigurator<TConsumer> where TConsumer : IRequestConsumer
{
    /// <inheritdoc />
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
