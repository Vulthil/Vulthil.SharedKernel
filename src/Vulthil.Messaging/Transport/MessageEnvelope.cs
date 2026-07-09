using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// On-the-wire wrapper for messages carried by a Vulthil transport. The body of every produced
/// message is a serialized <see cref="MessageEnvelope"/>; receivers fall back to bare JSON only for
/// compatibility with non-Vulthil producers.
/// </summary>
/// <remarks>
/// All metadata that would otherwise live in transport-specific headers is promoted to first-class
/// envelope fields, so the envelope is the source of truth and the wire format is transport-independent.
/// </remarks>
public sealed record MessageEnvelope
{
    /// <summary>The unique identifier for this message.</summary>
    [JsonPropertyName("messageId")]
    public string? MessageId { get; init; }

    /// <summary>For request/reply: the identifier of the request being answered, or that this message represents.</summary>
    [JsonPropertyName("requestId")]
    public string? RequestId { get; init; }

    /// <summary>Business correlation identifier shared across all messages in a logical operation.</summary>
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }

    /// <summary>Identifier shared by every message in the same conversation across services.</summary>
    [JsonPropertyName("conversationId")]
    public string? ConversationId { get; init; }

    /// <summary>The identifier of the message that initiated this chain.</summary>
    [JsonPropertyName("initiatorId")]
    public string? InitiatorId { get; init; }

    /// <summary>Address of the endpoint that produced this message.</summary>
    [JsonPropertyName("sourceAddress")]
    public string? SourceAddress { get; init; }

    /// <summary>Address of the endpoint this message was sent to (for point-to-point sends).</summary>
    [JsonPropertyName("destinationAddress")]
    public string? DestinationAddress { get; init; }

    /// <summary>Address where replies to this message should be sent.</summary>
    [JsonPropertyName("responseAddress")]
    public string? ResponseAddress { get; init; }

    /// <summary>Address where fault notifications should be sent.</summary>
    [JsonPropertyName("faultAddress")]
    public string? FaultAddress { get; init; }

    /// <summary>The stable wire URN identifying the message type, e.g. <c>urn:message:Acme.Orders:OrderPlaced</c>.</summary>
    [JsonPropertyName("messageType")]
    public required Uri MessageType { get; init; }

    /// <summary>The serialized payload. Receivers deserialize this into the CLR type resolved from <see cref="MessageType"/>.</summary>
    [JsonPropertyName("message")]
    public required JsonElement Message { get; init; }

    /// <summary>UTC timestamp when the message was sent.</summary>
    [JsonPropertyName("sentTime")]
    public DateTimeOffset? SentTime { get; init; }

    /// <summary>UTC timestamp after which the message should be discarded.</summary>
    [JsonPropertyName("expirationTime")]
    public DateTimeOffset? ExpirationTime { get; init; }

    /// <summary>
    /// Custom transport headers (e.g. tenancy markers, retry counters). Values travel as JSON, so receivers
    /// observe JSON primitives regardless of the published CLR type; the consume side normalizes them per the
    /// contract on <see cref="Abstractions.Consumers.IMessageContext.Headers"/>.
    /// </summary>
    [JsonPropertyName("headers")]
    public Dictionary<string, object?>? Headers { get; init; }
}
