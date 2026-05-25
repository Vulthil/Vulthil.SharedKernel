using System.Text.Json;
using Vulthil.Messaging.Queues;

namespace Vulthil.Messaging;

/// <summary>
/// Provides access to message configuration and a few transport-friendly helpers.
/// Implementations live in the messaging assembly and may call internal APIs.
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
}
