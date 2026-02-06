using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Queues;

public interface IQueueConfigurator
{
    IQueueConfigurator AddConsumer<TConsumer>(Action<ConsumerConfigurator<TConsumer>>? configure = null) where TConsumer : class, IConsumer;
    IQueueConfigurator ConfigureQueue(Action<QueueDefinition> configureAction);
}
