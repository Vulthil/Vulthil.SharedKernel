using System.Text.Json;

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
    /// <returns>The resolved <see cref="MessageConfiguration{TMessage}"/> instance.</returns>
    MessageConfiguration<TMessage> GetMessageConfiguration<TMessage>() where TMessage : class;

    /// <summary>
    /// Gets the JSON serializer options used by the messaging system.
    /// </summary>
    JsonSerializerOptions JsonSerializerOptions { get; }

    /// <summary>
    /// Gets the default request/response timeout used by transports.
    /// </summary>
    TimeSpan DefaultTimeout { get; }
}
