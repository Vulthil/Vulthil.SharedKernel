using Microsoft.Extensions.Hosting;
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
    /// Configures global messaging options such as serialization and timeouts.
    /// </summary>
    /// <param name="action">An action to configure the messaging options.</param>
    /// <returns>The current configurator instance for chaining.</returns>
    IMessagingConfigurator ConfigureMessagingOptions(Action<MessagingOptions> action);
}
