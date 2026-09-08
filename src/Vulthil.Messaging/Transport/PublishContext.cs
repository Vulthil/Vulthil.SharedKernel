using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// Mutable, write-through configuration captured for a single outgoing message. A transport creates one,
/// passes it to the caller's <c>configure</c> callback as <see cref="IPublishContext"/>, then reads the resolved
/// values to build its wire message. Metadata that maps to typed envelope fields is stored under reserved header
/// keys and surfaced through the strongly-typed properties.
/// </summary>
public class PublishContext : IPublishContext
{
    private readonly Dictionary<string, object?> _headers = [];

    /// <summary>Gets the custom and reserved headers accumulated for the message.</summary>
    public IReadOnlyDictionary<string, object?> Headers => _headers;
    /// <summary>Gets the routing key, or <see langword="null"/> if none was set.</summary>
    public string? RoutingKey { get; private set; }
    /// <summary>Gets the correlation identifier, or <see langword="null"/> if none was set.</summary>
    public string? CorrelationId { get; private set; }
    /// <summary>Gets the message identifier, or <see langword="null"/> if none was set.</summary>
    public string? MessageId { get; private set; }
    /// <summary>Gets the conversation identifier, or <see langword="null"/> if none was set.</summary>
    public string? ConversationId { get => GetString(MessageHeaders.ConversationId); private set => _headers[MessageHeaders.ConversationId] = value; }
    /// <summary>Gets the identifier of the message that initiated this chain, or <see langword="null"/> if none was set.</summary>
    public string? InitiatorId { get => GetString(MessageHeaders.InitiatorId); private set => _headers[MessageHeaders.InitiatorId] = value; }
    /// <summary>Gets or sets the address of the endpoint that produced the message; stamped by the transport.</summary>
    public Uri? SourceAddress { get => GetAddress(MessageHeaders.SourceAddress); set => SetAddress(MessageHeaders.SourceAddress, value); }
    /// <summary>Gets or sets the address of the endpoint the message is sent to; stamped by the transport.</summary>
    public Uri? DestinationAddress { get => GetAddress(MessageHeaders.DestinationAddress); set => SetAddress(MessageHeaders.DestinationAddress, value); }
    /// <summary>Gets the address where replies should be sent, or <see langword="null"/> if none was set.</summary>
    public Uri? ResponseAddress { get => GetAddress(MessageHeaders.ResponseAddress); private set => SetAddress(MessageHeaders.ResponseAddress, value); }
    /// <summary>Gets the address where fault notifications should be sent, or <see langword="null"/> if none was set.</summary>
    public Uri? FaultAddress { get => GetAddress(MessageHeaders.FaultAddress); private set => SetAddress(MessageHeaders.FaultAddress, value); }

    /// <inheritdoc />
    public void AddHeader(string key, object? value) => _headers[key] = value;
    /// <inheritdoc />
    public void AddHeaders(IDictionary<string, object?> headers)
    {
        foreach (var item in headers)
        {
            _headers[item.Key] = item.Value;
        }
    }
    /// <inheritdoc />
    public void SetRoutingKey(string routingKey) => RoutingKey = routingKey;
    /// <inheritdoc />
    public void SetCorrelationId(string correlationId) => CorrelationId = correlationId;
    /// <inheritdoc />
    public void SetMessageId(string messageId) => MessageId = messageId;
    /// <inheritdoc />
    public void SetConversationId(string conversationId) => ConversationId = conversationId;
    /// <inheritdoc />
    public void SetInitiatorId(string initiatorId) => InitiatorId = initiatorId;
    /// <inheritdoc />
    public void SetResponseAddress(Uri responseAddress) => ResponseAddress = responseAddress;
    /// <inheritdoc />
    public void SetFaultAddress(Uri faultAddress) => FaultAddress = faultAddress;

    private string? GetString(string key) => _headers.TryGetValue(key, out var value) && value is string stored ? stored : null;

    private Uri? GetAddress(string key) => MessageAddress.Parse(GetString(key));

    private void SetAddress(string key, Uri? address) => _headers[key] = address is null ? null : MessageAddress.ToHeaderValue(address);
}
