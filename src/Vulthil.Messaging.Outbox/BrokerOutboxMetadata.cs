using System.Text.Json.Serialization;

namespace Vulthil.Messaging.Outbox;

/// <summary>
/// The broker-publish metadata persisted (as JSON) alongside a captured outbox message, so the relay can reproduce
/// the original publish: the stable message id, correlation, routing key, send destination, and headers.
/// </summary>
internal sealed record BrokerOutboxMetadata
{
    public string? MessageId { get; init; }

    public string? CorrelationId { get; init; }

    public string? RoutingKey { get; init; }

    public string? DestinationAddress { get; init; }

    /// <summary>
    /// The captured publish headers. Values persist as JSON, so the relay reproduces JSON-primitive values
    /// exactly and republishes anything else as its serialized JSON form — a relayed message's headers match
    /// a directly-published message's on the wire, but CLR types with no JSON representation (e.g.
    /// <see cref="Guid"/>) are not rematerialized as their original type.
    /// </summary>
    [JsonConverter(typeof(BrokerOutboxMetadataHeadersConverter))]
    public Dictionary<string, object?>? Headers { get; init; }
}
