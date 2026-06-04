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
    public string? ConversationId { get => _headers.TryGetValue("ConversationId", out var value) && value is string conversationId ? conversationId : null; private set => _headers["ConversationId"] = value; }
    /// <summary>Gets the identifier of the message that initiated this chain, or <see langword="null"/> if none was set.</summary>
    public string? InitiatorId { get => _headers.TryGetValue("InitiatorId", out var value) && value is string initiatorId ? initiatorId : null; private set => _headers["InitiatorId"] = value; }
    /// <summary>Gets or sets the address of the endpoint that produced the message; stamped by the transport.</summary>
    public Uri? SourceAddress { get => _headers.TryGetValue("SourceAddress", out var value) && value is string sourceAddress ? new Uri(sourceAddress) : null; set => _headers["SourceAddress"] = MapUriToString(value); }
    /// <summary>Gets or sets the address of the endpoint the message is sent to; stamped by the transport.</summary>
    public Uri? DestinationAddress { get => _headers.TryGetValue("DestinationAddress", out var value) && value is string destinationAddress ? new Uri(destinationAddress) : null; set => _headers["DestinationAddress"] = MapUriToString(value); }
    /// <summary>Gets the address where replies should be sent, or <see langword="null"/> if none was set.</summary>
    public Uri? ResponseAddress { get => _headers.TryGetValue("ResponseAddress", out var value) && value is string responseAddress ? new Uri(responseAddress) : null; private set => _headers["ResponseAddress"] = MapUriToString(value); }
    /// <summary>Gets the address where fault notifications should be sent, or <see langword="null"/> if none was set.</summary>
    public Uri? FaultAddress { get => _headers.TryGetValue("FaultAddress", out var value) && value is string faultAddress ? new Uri(faultAddress) : null; private set => _headers["FaultAddress"] = MapUriToString(value); }

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

    private static string? MapUriToString(Uri? uri)
    {
        if (uri is null)
        {
            return null;
        }

        return uri.Scheme == "queue" ? uri.LocalPath.TrimStart('/') : uri.ToString();
    }
}
