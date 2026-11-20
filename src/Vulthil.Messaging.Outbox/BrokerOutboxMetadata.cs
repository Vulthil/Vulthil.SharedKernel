using System.Text.Json;

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
    /// The captured publish headers as JSON (see <see cref="CaptureHeaders"/>). The relay restores them with
    /// <see cref="RestoreHeaders"/>, so a relayed message's headers match a directly-published message's on the wire,
    /// but CLR types with no JSON representation (e.g. <see cref="Guid"/>) are not rematerialized as their original
    /// type.
    /// </summary>
    public Dictionary<string, JsonElement>? Headers { get; init; }

    public static Dictionary<string, JsonElement>? CaptureHeaders(IReadOnlyDictionary<string, object?> headers, JsonSerializerOptions options)
        => headers.Count == 0
            ? null
            : headers.ToDictionary(header => header.Key, header => JsonSerializer.SerializeToElement(header.Value, options));

    public Dictionary<string, object?>? RestoreHeaders()
        => Headers?.ToDictionary(header => header.Key, header => ToHeaderValue(header.Value));

    // The rule of HeaderValueNormalizer, which is internal to Vulthil.Messaging: a relayed header reaches the transport
    // as the CLR primitive a consumer normalizes it to (the narrowest number type that holds the value), so the relay
    // writes the same wire bytes as a direct publish. Objects and arrays stay JSON and serialize back verbatim.
    private static object? ToHeaderValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Number when value.TryGetInt32(out var intValue) => intValue,
        JsonValueKind.Number when value.TryGetInt64(out var longValue) => longValue,
        JsonValueKind.Number => value.GetDouble(),
        _ => value,
    };
}
