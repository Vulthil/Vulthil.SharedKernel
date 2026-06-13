using System.Text.Json;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// Builds <see cref="MessageEnvelope"/> instances from the resolved publish state for a single outgoing message.
/// Promotes the reserved metadata headers carried by a <see cref="PublishContext"/> to typed envelope fields and
/// copies the remaining custom headers verbatim.
/// </summary>
public static class MessageEnvelopeFactory
{
    private static readonly HashSet<string> PromotedHeaderKeys = new(StringComparer.Ordinal)
    {
        "ConversationId",
        "InitiatorId",
        "SourceAddress",
        "DestinationAddress",
        "ResponseAddress",
        "FaultAddress",
    };

    /// <summary>
    /// Builds a <see cref="MessageEnvelope"/> from the resolved publish state for a single outgoing message.
    /// </summary>
    /// <typeparam name="TMessage">The message type being published.</typeparam>
    /// <param name="message">The message payload to serialize into the envelope.</param>
    /// <param name="publishContext">The resolved publish configuration (addresses, headers, conversation metadata).</param>
    /// <param name="messageId">The unique identifier assigned to the message.</param>
    /// <param name="correlationId">The business correlation identifier.</param>
    /// <param name="urn">The stable wire URN identifying the message type.</param>
    /// <param name="jsonOptions">The serializer options used to serialize the payload.</param>
    /// <param name="requestId">For request/reply, the request identifier the reply echoes; otherwise <see langword="null"/>.</param>
    /// <returns>The constructed envelope.</returns>
    public static MessageEnvelope Create<TMessage>(
        TMessage message,
        PublishContext publishContext,
        string messageId,
        string correlationId,
        Uri urn,
        JsonSerializerOptions jsonOptions,
        string? requestId = null)
        where TMessage : notnull
    {
        // Copy user headers, removing the keys that we promote to typed envelope fields.
        Dictionary<string, object?>? userHeaders = null;
        foreach (var (key, value) in publishContext.Headers)
        {
            if (PromotedHeaderKeys.Contains(key))
            {
                continue;
            }
            userHeaders ??= new Dictionary<string, object?>(StringComparer.Ordinal);
            userHeaders[key] = value;
        }

        return new MessageEnvelope
        {
            MessageId = messageId,
            RequestId = requestId,
            CorrelationId = correlationId,
            ConversationId = publishContext.ConversationId,
            InitiatorId = publishContext.InitiatorId,
            SourceAddress = publishContext.SourceAddress?.ToString(),
            DestinationAddress = publishContext.DestinationAddress?.ToString(),
            ResponseAddress = publishContext.ResponseAddress?.ToString(),
            FaultAddress = publishContext.FaultAddress?.ToString(),
            MessageType = urn,
            Message = JsonSerializer.SerializeToElement(message, jsonOptions),
            SentTime = DateTimeOffset.UtcNow,
            Headers = userHeaders,
        };
    }
}
