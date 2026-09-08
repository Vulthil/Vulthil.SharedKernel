namespace Vulthil.Messaging.Transport;

/// <summary>
/// The reserved header keys under which a <see cref="PublishContext"/> carries the metadata that
/// <see cref="MessageEnvelopeFactory"/> promotes to typed <see cref="MessageEnvelope"/> fields and that a transport
/// reads back on the consume side. A custom header must not use one of these keys.
/// </summary>
public static class MessageHeaders
{
    /// <summary>The identifier shared by every message in the same conversation.</summary>
    public const string ConversationId = "ConversationId";

    /// <summary>The identifier of the message that initiated the chain.</summary>
    public const string InitiatorId = "InitiatorId";

    /// <summary>The address of the endpoint that produced the message.</summary>
    public const string SourceAddress = "SourceAddress";

    /// <summary>The address the message was sent to.</summary>
    public const string DestinationAddress = "DestinationAddress";

    /// <summary>The address replies should be sent to.</summary>
    public const string ResponseAddress = "ResponseAddress";

    /// <summary>The address fault notifications should be sent to.</summary>
    public const string FaultAddress = "FaultAddress";

    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        ConversationId,
        InitiatorId,
        SourceAddress,
        DestinationAddress,
        ResponseAddress,
        FaultAddress,
    };

    /// <summary>
    /// Returns whether <paramref name="key"/> is one of the reserved header keys. The comparison is ordinal.
    /// </summary>
    /// <param name="key">The header key to test.</param>
    /// <returns><see langword="true"/> when the key is reserved; otherwise <see langword="false"/>.</returns>
    public static bool IsReserved(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Reserved.Contains(key);
    }
}
