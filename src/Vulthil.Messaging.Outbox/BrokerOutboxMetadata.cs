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

    public Dictionary<string, object?>? Headers { get; init; }
}
