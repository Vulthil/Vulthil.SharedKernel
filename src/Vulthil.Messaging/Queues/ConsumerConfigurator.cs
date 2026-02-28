using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Queues;

public abstract class BaseConfigurator
{
    internal Dictionary<MessageType, string> Overrides { get; } = [];

    internal RetryPolicyDefinition? RetryPolicy { get; private set; }

    public void UseRetry(Action<RetryPolicyConfigurator> value)
    {
        var _retryPolicyConfigurator = new RetryPolicyConfigurator();
        value(_retryPolicyConfigurator);
        RetryPolicy = _retryPolicyConfigurator.Build();
    }
}

public sealed class ConsumerConfigurator<TConsumer> : BaseConfigurator, IConsumerConfigurator<TConsumer> where TConsumer : IConsumer
{
    public IConsumerConfigurator<TConsumer> Bind<TMessage>(string routingKey)
        where TMessage : notnull
    {
        if (!typeof(TConsumer).IsAssignableTo(typeof(IConsumer<TMessage>)))
        {
            throw new ArgumentException(
                $"Registration Error: '{typeof(TConsumer).Name}' cannot bind to '{typeof(TMessage).Name}' " +
                $"because it does not implement IConsumer<{typeof(TMessage).Name}>.");
        }

        Overrides[new(typeof(TMessage))] = routingKey;
        return this;
    }

}

public interface IConsumerConfigurator<TConsumer> where TConsumer : IConsumer
{
    IConsumerConfigurator<TConsumer> Bind<TMessage>(string routingKey)
        where TMessage : notnull;
    void UseRetry(Action<RetryPolicyConfigurator> value);
}
public interface IRequestConfigurator<TConsumer> where TConsumer : IRequestConsumer
{
    IRequestConfigurator<TConsumer> Bind<TRequest, TResponse>(string routingKey)
        where TRequest : notnull
        where TResponse : notnull;
    void UseRetry(Action<RetryPolicyConfigurator> value);
}
