using System.Text.Json;
using Vulthil.Messaging.RabbitMq.Requests;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.RabbitMq.Envelope;

internal static class MessageEnvelopeFactory
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
