namespace Vulthil.Messaging.Abstractions.Publishers;

/// <summary>
/// Provides a write-only context for configuring outgoing message properties. Every member sets a value on the
/// message being published; nothing is read back through this interface.
/// </summary>
public interface IPublishContext
{
    /// <summary>
    /// Sets the routing key for the published message.
    /// </summary>
    /// <param name="routingKey">The routing key.</param>
    void SetRoutingKey(string routingKey);
    /// <summary>
    /// Sets the correlation identifier for the published message.
    /// </summary>
    /// <param name="correlationId">The correlation identifier.</param>
    void SetCorrelationId(string correlationId);
    /// <summary>
    /// Sets the unique message identifier assigned to the outgoing message.
    /// </summary>
    /// <param name="messageId">The message identifier.</param>
    void SetMessageId(string messageId);
    /// <summary>
    /// Sets the conversation identifier that groups related messages across services.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    void SetConversationId(string conversationId);
    /// <summary>
    /// Sets the identifier of the message that initiated this chain.
    /// </summary>
    /// <param name="initiatorId">The initiator identifier.</param>
    void SetInitiatorId(string initiatorId);
    /// <summary>
    /// Sets the address where replies to this message should be sent.
    /// </summary>
    /// <param name="responseAddress">The response address.</param>
    void SetResponseAddress(Uri responseAddress);
    /// <summary>
    /// Sets the address where fault notifications for this message should be sent.
    /// </summary>
    /// <param name="faultAddress">The fault address.</param>
    void SetFaultAddress(Uri faultAddress);
    /// <summary>
    /// Adds a custom header to the published message.
    /// </summary>
    /// <param name="key">The header key.</param>
    /// <param name="value">The header value.</param>
    void AddHeader(string key, object? value);
    /// <summary>
    /// Adds multiple custom headers to the published message.
    /// </summary>
    /// <param name="headers">The headers to add.</param>
    void AddHeaders(IDictionary<string, object?> headers);
}
