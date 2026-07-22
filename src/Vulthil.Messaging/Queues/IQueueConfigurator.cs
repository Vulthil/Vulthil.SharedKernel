using System.Reflection;
using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Queues;

/// <summary>
/// Configures a queue's consumers, retry policies, and dead letter settings.
/// </summary>
public interface IQueueConfigurator
{
    /// <summary>
    /// Registers a one-way consumer on this queue with optional per-consumer configuration.
    /// If the consumer's <c>TMessage</c> is concrete, the queue is auto-subscribed to it at build time;
    /// for polymorphic consumers (e.g. <c>IConsumer&lt;IOrderEvent&gt;</c>) the caller must explicitly
    /// <see cref="Subscribe{TMessage}"/> the concrete implementers.
    /// </summary>
    IQueueConfigurator AddConsumer<TConsumer>(Action<IConsumerConfigurator<TConsumer>>? configure = null) where TConsumer : class, IConsumer;
    /// <summary>
    /// Registers a request/reply consumer on this queue with optional per-consumer configuration.
    /// Request consumers do not retry — a thrown exception is returned to the requester as an RPC fault
    /// reply — so configuring <see cref="IBaseConfigurator{TConfigurator}.UseRetry"/> on
    /// <paramref name="configure"/> throws at configuration time.
    /// </summary>
    IQueueConfigurator AddRequestConsumer<TConsumer>(Action<IRequestConfigurator<TConsumer>>? configure = null) where TConsumer : class, IRequestConsumer;

    /// <summary>
    /// Subscribes this queue to receive deliveries of the concrete message type <typeparamref name="TMessage"/>.
    /// At topology setup time, the queue is bound to <typeparamref name="TMessage"/>'s exchange with the supplied
    /// <paramref name="routingKey"/> pattern (the broker uses this to filter; the worker does not re-filter).
    /// Abstract types and interfaces are rejected — those have no exchange; use <see cref="SubscribeAll{TInterface}"/>
    /// or call <see cref="Subscribe{TMessage}"/> for each concrete implementer instead.
    /// </summary>
    /// <typeparam name="TMessage">A concrete (non-abstract, non-interface) message type.</typeparam>
    /// <param name="routingKey">
    /// The binding pattern. When <see langword="null"/>, the broker receives an empty string —
    /// fanout/headers exchanges ignore it; direct exchanges only deliver messages with an empty
    /// published routing key; topic exchanges match no patterns. For non-empty needs, supply a specific
    /// pattern (e.g. <c>"order.created"</c> for direct, <c>"order.*"</c> for topic).
    /// </param>
    IQueueConfigurator Subscribe<TMessage>(string? routingKey = null) where TMessage : class;

    /// <summary>
    /// Discovers every concrete (non-abstract, non-interface) type in <paramref name="assembly"/> that is
    /// assignable to <typeparamref name="TInterface"/>, and calls <see cref="Subscribe{TMessage}"/> for each.
    /// Pair with a polymorphic <c>AddConsumer&lt;TConsumer&gt;()</c> (where <c>TConsumer : IConsumer&lt;TInterface&gt;</c>)
    /// to dispatch all implementers through one consumer.
    /// </summary>
    /// <typeparam name="TInterface">The polymorphic dispatch type — typically an interface or abstract base class.</typeparam>
    /// <param name="assembly">The assembly to scan for concrete implementers.</param>
    /// <param name="routingKey">The binding pattern applied to every discovered implementer's exchange. <see langword="null"/> = broker default.</param>
    IQueueConfigurator SubscribeAll<TInterface>(Assembly assembly, string? routingKey = null) where TInterface : class;

    /// <summary>
    /// Applies additional configuration to the underlying <see cref="QueueDefinition"/>.
    /// </summary>
    IQueueConfigurator ConfigureQueue(Action<QueueDefinition> configureAction);

    /// <summary>
    /// Configures the default retry policy for this queue's one-way consumers. A consumer-level
    /// <see cref="IBaseConfigurator{TConfigurator}.UseRetry"/> takes precedence over this default.
    /// Request consumers never retry (a thrown exception is returned to the requester as an RPC
    /// fault reply), so this default does not apply to them.
    /// </summary>
    IQueueConfigurator UseRetry(Action<RetryPolicyConfigurator> configure);
    /// <summary>
    /// Enables a dead letter queue for this queue.
    /// </summary>
    /// <param name="queueName">Optional dead letter queue name.</param>
    /// <param name="exchangeName">Optional dead letter exchange name.</param>
    IQueueConfigurator UseDeadLetterQueue(string? queueName = null, string? exchangeName = null);

    /// <summary>
    /// Declares the queue with RabbitMQ's single active consumer feature: only one consumer is active at a
    /// time while additional consumers stand by and take over on failure. This preserves per-queue order
    /// across load-balanced consumer instances — extending the in-process partitioner's ordering guarantee
    /// to multiple instances — at the cost of throughput scale-out for the queue. Partitioned queues enable
    /// this automatically.
    /// </summary>
    IQueueConfigurator UseSingleActiveConsumer();
}
