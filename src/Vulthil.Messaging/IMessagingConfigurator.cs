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
    IMessagingConfigurator AddQueue(string queueName, Action<IQueueConfigurator> queueConfigurationAction);

    /// <summary>
    /// Configures global messaging options such as serialization and timeouts.
    /// </summary>
    /// <param name="action">An action to configure the messaging options.</param>
    /// <returns>The current configurator instance for chaining.</returns>
    IMessagingConfigurator ConfigureMessagingOptions(Action<MessagingOptions> action);
    /// <summary>
    /// Configures fault handling infrastructure including exchange, queue, and consumer bindings.
    /// </summary>
    /// <param name="configureFaults">An action to configure fault handling.</param>
    /// <returns>The current configurator instance for chaining.</returns>
    IMessagingConfigurator ConfigureFaults(Action<IFaultConfigurator> configureFaults);

    /// <summary>
    /// Registers a function that extracts the routing key from a message of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="picker">A function that produces the routing key from the message.</param>
    /// <returns>The current configurator instance for chaining.</returns>
    IMessagingConfigurator RegisterRoutingKeyFormatter<T>(Func<T, string> picker) where T : class;
    /// <summary>
    /// Registers a fixed routing key for messages of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="routingKey">The fixed routing key to use.</param>
    /// <returns>The current configurator instance for chaining.</returns>
    IMessagingConfigurator RegisterRoutingKeyFormatter<T>(string routingKey) where T : class;

    /// <summary>
    /// Registers a function that extracts the correlation identifier from a message of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="picker">A function that produces the correlation identifier from the message.</param>
    /// <returns>The current configurator instance for chaining.</returns>
    IMessagingConfigurator RegisterCorrelationIdFormatter<T>(Func<T, string> picker) where T : class;

}
/// <summary>
/// Configures fault handling infrastructure for a messaging service.
/// </summary>
public interface IFaultConfigurator
{
    /// <summary>
    /// Sets the name of the global topic exchange for faults. Default is "Fault.Exchange".
    /// </summary>
    void UseExchange(string exchangeName);

    /// <summary>
    /// Defines the local queue name that will receive faults. 
    /// If not set, a default name (e.g., "service-name.faults") is used.
    /// </summary>
    void UseQueue(string queueName);

    /// <summary>
    /// Adds a consumer to handle faults received by this service.
    /// </summary>
    void AddConsumer<TConsumer>() where TConsumer : class, IConsumer;

    /// <summary>
    /// Sets the routing keys to listen for (e.g. "#", "Order.*"). Default is "#".
    /// </summary>
    void Bind(params string[] routingKeys);
}
