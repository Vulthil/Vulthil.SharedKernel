using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Queues;

/// <summary>
/// Configures per-request-consumer settings. Binding patterns live on
/// <see cref="IQueueConfigurator.Subscribe{TMessage}"/>; routing-key formatters for the producer side
/// live on <see cref="MessageConfiguration{TMessage}"/>. Configuring
/// <see cref="IBaseConfigurator{TConfigurator}.UseRetry"/> here throws at configuration time: request
/// consumers do not retry — a thrown exception is returned to the requester as an RPC fault reply.
/// </summary>
/// <typeparam name="TConsumer">The request consumer type; reserved on the interface to keep parity with <see cref="IQueueConfigurator.AddRequestConsumer{TConsumer}"/> and future per-consumer-type knobs.</typeparam>
public interface IRequestConfigurator<TConsumer> : IBaseConfigurator<IRequestConfigurator<TConsumer>>
    where TConsumer : IRequestConsumer;
