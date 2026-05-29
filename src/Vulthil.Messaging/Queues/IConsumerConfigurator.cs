using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Queues;

/// <summary>
/// Configures per-consumer settings. Binding patterns live on
/// <see cref="IQueueConfigurator.Subscribe{TMessage}"/>; routing-key formatters for the producer side
/// live on <see cref="MessageConfiguration{TMessage}"/>.
/// </summary>
/// <typeparam name="TConsumer">The consumer type; reserved on the interface to keep parity with <see cref="IQueueConfigurator.AddConsumer{TConsumer}"/> and to leave a hook for future per-consumer-type knobs.</typeparam>
public interface IConsumerConfigurator<TConsumer> : IBaseConfigurator<IConsumerConfigurator<TConsumer>>
    where TConsumer : IConsumer;
