using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.RabbitMq.Consumers;

/// <summary>
/// Builds the <see cref="MessageContext{TMessage}"/> of a RabbitMQ delivery. An envelope delivery takes its metadata
/// from the envelope (see <see cref="MessageContext.CreateFromEnvelope{TMessage}"/>) and only the transport fields from
/// the delivery; a bare-JSON delivery takes all of it from the AMQP properties and headers. Header values are
/// normalized via <see cref="AmqpHeaderValueNormalizer"/> so both paths honor the <see cref="IMessageContext.Headers"/>
/// contract, and a per-message TTL is anchored to the delivery's timestamp when one is present (the AMQP expiration is
/// relative to publish, not to consumption).
/// </summary>
internal static class MessageContextFactory
{
    public static MessageContext<TMessage> CreateContext<TMessage>(
        TMessage message,
        BasicDeliverEventArgs ea,
        MessageEnvelope? envelope = null,
        IPublisher? publisher = null,
        ISendEndpointProvider? sendEndpointProvider = null,
        CancellationToken cancellationToken = default)
    {
        var props = ea.BasicProperties;
        var headers = props.Headers ?? new Dictionary<string, object?>();
        if (envelope is not null)
        {
            return MessageContext.CreateFromEnvelope(
                message,
                envelope,
                ea.RoutingKey,
                ea.Redelivered,
                RabbitMqConstants.GetRetryCount(headers),
                props.ReplyTo,
                publisher,
                sendEndpointProvider,
                cancellationToken);
        }

        DateTimeOffset? sentTime = props.Timestamp.UnixTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(props.Timestamp.UnixTime) : null;
        return new MessageContext<TMessage>
        {
            Message = message,
            Publisher = publisher,
            SendEndpointProvider = sendEndpointProvider,
            CancellationToken = cancellationToken,
            MessageId = props.MessageId,
            CorrelationId = props.CorrelationId,
            RequestId = props.CorrelationId,
            RoutingKey = ea.RoutingKey,
            Headers = NormalizeCustomHeaders(headers),
            Redelivered = ea.Redelivered,
            RetryCount = RabbitMqConstants.GetRetryCount(headers),
            ConversationId = RabbitMqConstants.GetHeaderString(headers, MessageHeaders.ConversationId),
            InitiatorId = RabbitMqConstants.GetHeaderString(headers, MessageHeaders.InitiatorId),
            SourceAddress = RabbitMqConstants.GetHeaderUri(headers, MessageHeaders.SourceAddress),
            DestinationAddress = RabbitMqConstants.GetHeaderUri(headers, MessageHeaders.DestinationAddress),
            ResponseAddress = RabbitMqConstants.GetHeaderUri(headers, MessageHeaders.ResponseAddress)
                ?? (string.IsNullOrEmpty(props.ReplyTo) ? null : MessageAddress.Queue(props.ReplyTo)),
            FaultAddress = RabbitMqConstants.GetHeaderUri(headers, MessageHeaders.FaultAddress),
            SentTime = sentTime,
            ExpirationTime = RabbitMqConstants.TryParseExpiration(props.Expiration, sentTime),
        };
    }

    /// <summary>
    /// Normalizes the delivery's headers for <see cref="IMessageContext.Headers"/> without the
    /// <see cref="MessageHeaders"/> reserved keys: the context carries those as typed properties, and the envelope path
    /// leaves them out of the headers too, because the producer promotes them to envelope fields.
    /// </summary>
    private static Dictionary<string, object?> NormalizeCustomHeaders(IDictionary<string, object?> headers)
    {
        var customHeaders = AmqpHeaderValueNormalizer.Normalize(headers);
        foreach (var key in headers.Keys.Where(MessageHeaders.IsReserved))
        {
            customHeaders.Remove(key);
        }

        return customHeaders;
    }
}
