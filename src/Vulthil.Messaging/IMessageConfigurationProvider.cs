using System.Text.Json;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging;

/// <summary>
/// Read-only view of the messaging options resolved at runtime by transports, consumers, and filters.
/// Backed by the same instance that <see cref="IMessagingOptionsConfigurator"/> writes to during composition.
/// </summary>
public interface IMessageConfigurationProvider
{
    /// <summary>
    /// Gets the message configuration for the specified message type.
    /// </summary>
    /// <param name="messageType">The message CLR type.</param>
    /// <returns>The resolved <see cref="MessageConfiguration"/> instance.</returns>
    MessageConfiguration GetMessageConfiguration(Type messageType);

    /// <summary>
    /// Gets the message configuration for the specified generic message type.
    /// </summary>
    /// <typeparam name="TMessage">The message CLR type.</typeparam>
    /// <returns>The resolved <see cref="MessageConfiguration"/> instance.</returns>
    MessageConfiguration GetMessageConfiguration<TMessage>() where TMessage : class;

    /// <summary>
    /// Gets the JSON serializer options used by the messaging system.
    /// </summary>
    JsonSerializerOptions JsonSerializerOptions { get; }

    /// <summary>
    /// Gets the default request/response timeout used by transports.
    /// </summary>
    TimeSpan DefaultTimeout { get; }

    /// <summary>
    /// Gets the name of the exchange used for fault messages.
    /// </summary>
    string FaultExchangeName { get; }

    /// <summary>
    /// Gets the queue definitions that were assembled from configuration and code at <c>AddMessaging</c> time.
    /// Returned as a snapshot; subsequent mutations to the underlying store are not reflected.
    /// </summary>
    IReadOnlyCollection<QueueDefinition> QueueDefinitions { get; }

    /// <summary>
    /// Gets the options controlling which built-in consume filters perform their work at runtime.
    /// Filters check the appropriate flag on this object on every delivery, so toggles take effect
    /// without re-registering the filter.
    /// </summary>
    ConsumeFilterOptions ConsumeFilters { get; }
}
