using System.Text.Json.Serialization;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// Wire payload for an RPC failure reply. Carried as the <c>message</c> of a <see cref="MessageEnvelope"/>
/// whose <c>messageType</c> is <see cref="UrnUri"/>. Property names are fixed (camelCase) so the reply
/// round-trips regardless of the configured JSON naming policy.
/// </summary>
public sealed record RpcFault
{
    /// <summary>The stable wire URN identifying an RPC fault reply payload.</summary>
    public const string Urn = "urn:message:Vulthil:RpcFault";

    /// <summary>The <see cref="Urn"/> as a <see cref="Uri"/>, for comparison against an envelope's message type.</summary>
    public static Uri UrnUri { get; } = new(Urn);

    /// <summary>The exception message describing the failure.</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>The fully-qualified type name of the exception.</summary>
    [JsonPropertyName("exceptionType")]
    public required string ExceptionType { get; init; }

    /// <summary>The stack trace of the exception, or <see langword="null"/> if unavailable.</summary>
    [JsonPropertyName("stackTrace")]
    public string? StackTrace { get; init; }

    /// <summary>The UTC timestamp when the fault occurred.</summary>
    [JsonPropertyName("faultedAt")]
    public required DateTimeOffset FaultedAt { get; init; }
}
