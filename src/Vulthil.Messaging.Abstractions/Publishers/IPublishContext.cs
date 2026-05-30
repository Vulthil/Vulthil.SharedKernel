namespace Vulthil.Messaging.Abstractions.Publishers;

/// <summary>
/// Provides a mutable context for configuring outgoing message properties.
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
    /// <summary>
    /// Gets or sets the unique message identifier assigned to the outgoing message.
    /// </summary>
    string? MessageId { get; set; }
    /// <summary>
    /// Gets or sets the conversation identifier that groups related messages across services.
    /// </summary>
    string? ConversationId { get; set; }
    /// <summary>
    /// Gets or sets the identifier of the message that initiated this chain.
    /// </summary>
    string? InitiatorId { get; set; }
    /// <summary>
    /// Gets or sets the address where replies to this message should be sent.
    /// </summary>
    Uri? ResponseAddress { get; set; }
    /// <summary>
    /// Gets or sets the address where fault notifications for this message should be sent.
    /// </summary>
    Uri? FaultAddress { get; set; }
}
