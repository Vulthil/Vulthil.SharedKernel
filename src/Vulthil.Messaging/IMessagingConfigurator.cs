using Microsoft.Extensions.Hosting;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging;

/// <summary>
/// Fluent configurator for registering messaging queues, consumers, and options.
/// </summary>
public interface IMessagingConfigurator
{
    /// <summary>
    /// Gets the host application builder used for service registration during messaging configuration.
    /// </summary>
    IHostApplicationBuilder HostApplicationBuilder { get; }
    /// <summary>
    /// Adds a named queue with consumer and routing configuration.
    /// </summary>
    /// <param name="queueName">The name of the queue.</param>
    /// <param name="queueConfigurationAction">An action to configure the queue's consumers and settings.</param>
    /// <returns>The current configurator instance for chaining.</returns>
    IMessagingConfigurator ConfigureQueue(string queueName, Action<IQueueConfigurator> queueConfigurationAction);

    /// <summary>
    /// Registers a publish definition for messages of type <typeparamref name="TMessage"/>. This allows you to specify exchange and routing configurations for outgoing messages of that type.
    /// </summary>
    /// <typeparam name="TMessage">The type of the message to publish.</typeparam>
    /// <param name="configureMessageAction">An action to configure the message's publish settings.</param>
    /// <returns>The current configurator instance for chaining.</returns>
    IMessagingConfigurator ConfigureMessage<TMessage>(Action<MessageConfiguration<TMessage>> configureMessageAction)
        where TMessage : class;

    /// <summary>
    /// Configures global messaging options such as serialization, timeouts, and built-in filters.
    /// </summary>
    /// <param name="action">An action that receives the writable options surface.</param>
    /// <returns>The current configurator instance for chaining.</returns>
    IMessagingConfigurator ConfigureMessagingOptions(Action<IMessagingOptionsConfigurator> action);

    /// <summary>
    /// Registers a closed-generic consume filter. The filter is applied to every delivery whose
    /// message type matches the filter's <c>IConsumeFilter&lt;TMessage&gt;</c> interface.
    /// Multiple filters for the same message type are composed in registration order
    /// (first registered is outermost).
    /// </summary>
    /// <typeparam name="TFilter">The filter implementation. Must implement at least one <c>IConsumeFilter&lt;TMessage&gt;</c>.</typeparam>
    /// <returns>The current configurator instance for chaining.</returns>
    IMessagingConfigurator AddConsumeFilter<TFilter>() where TFilter : class;

    /// <summary>
    /// Registers an open-generic consume filter that applies to every message type. Use this for
    /// cross-cutting filters that do not depend on the typed payload (logging, telemetry, etc.).
    /// </summary>
    /// <param name="openFilterType">An open generic type (e.g. <c>typeof(LoggingFilter&lt;&gt;)</c>) implementing <c>IConsumeFilter&lt;&gt;</c>.</param>
    /// <returns>The current configurator instance for chaining.</returns>
    IMessagingConfigurator AddOpenConsumeFilter(Type openFilterType);

    /// <summary>
    /// Serializes processing of <typeparamref name="TMessage"/> deliveries that share a partition key, so
    /// messages correlated to the same aggregate are handled one at a time and in order, while messages with
    /// different keys process concurrently. Effective only when the consuming queue allows concurrency
    /// (<c>ConcurrencyLimit &gt; 1</c>); at a concurrency of one, processing is already serial.
    /// </summary>
    /// <typeparam name="TMessage">The message type to partition.</typeparam>
    /// <param name="partitionCount">The number of partitions (lanes) to distribute keys across.</param>
    /// <param name="keySelector">
    /// Selects the partition key from a delivery (e.g. <c>ctx =&gt; ctx.CorrelationId</c>). A delivery whose
    /// key is <see langword="null"/> or empty bypasses the partitioner.
    /// </param>
    /// <returns>The current configurator instance for chaining.</returns>
    IMessagingConfigurator UsePartitioner<TMessage>(int partitionCount, Func<IMessageContext<TMessage>, string?> keySelector)
        where TMessage : notnull;

    /// <summary>
    /// Serializes processing of <typeparamref name="TMessage"/> deliveries that share a partition key using a
    /// caller-supplied <see cref="Partitioner"/>. Share one <see cref="Partitioner"/> across several message
    /// types to serialize messages correlated to the same key regardless of their type (e.g. a saga).
    /// </summary>
    /// <typeparam name="TMessage">The message type to partition.</typeparam>
    /// <param name="partitioner">The partitioner whose lanes serialize same-key processing.</param>
    /// <param name="keySelector">
    /// Selects the partition key from a delivery (e.g. <c>ctx =&gt; ctx.CorrelationId</c>). A delivery whose
    /// key is <see langword="null"/> or empty bypasses the partitioner.
    /// </param>
    /// <returns>The current configurator instance for chaining.</returns>
    IMessagingConfigurator UsePartitioner<TMessage>(Partitioner partitioner, Func<IMessageContext<TMessage>, string?> keySelector)
        where TMessage : notnull;
}
