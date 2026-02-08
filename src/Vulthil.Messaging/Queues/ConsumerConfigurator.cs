using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Queues;

public class ConsumerConfigurator<TConsumer> where TConsumer : IConsumer
{
    internal Dictionary<MessageType, string> Overrides { get; } = [];

    public ConsumerConfigurator<TConsumer> Bind<TMessage>(string routingKey)
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

