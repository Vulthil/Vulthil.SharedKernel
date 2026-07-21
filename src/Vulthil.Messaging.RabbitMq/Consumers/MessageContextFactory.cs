using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.Transport;

namespace Vulthil.Messaging.RabbitMq.Consumers;

/// <summary>
/// Builds <see cref="MessageContext"/> instances from RabbitMQ deliveries: the AMQP property/header paths plus a
/// serializable snapshot for faults. The envelope receive path delegates the transport-agnostic mapping to
/// <see cref="MessageContext.CreateFromEnvelope{TMessage}"/>. Header values are normalized via
/// <see cref="AmqpHeaderValueNormalizer"/> so both receive paths honor the <see cref="IMessageContext.Headers"/>
/// contract, and a per-message TTL is anchored to the delivery's timestamp when one is present (the AMQP
/// expiration is relative to publish, not to consumption).
/// </summary>
internal static class MessageContextFactory
{
    /// <summary>
    /// Creates a snapshot typed <see cref="MessageContext{TMessage}"/> with no live transport binding.
    /// </summary>
    public static MessageContext<TMessage> CreateContext<TMessage>(TMessage message, BasicDeliverEventArgs ea) =>
        BuildTypedMetadata(message, ea, publisher: null, sendEndpointProvider: null, cancellationToken: default);

    /// <summary>
    /// Creates a live typed <see cref="MessageContext{TMessage}"/> bound to the specified transport services and cancellation token.
    /// Used by the bare-JSON receive path; metadata comes from <paramref name="ea"/> and its <c>BasicProperties</c> headers.
    /// </summary>
    public static MessageContext<TMessage> CreateContext<TMessage>(
        TMessage message,
        BasicDeliverEventArgs ea,
        IPublisher? publisher,
        ISendEndpointProvider? sendEndpointProvider,
        CancellationToken cancellationToken) =>
        BuildTypedMetadata(message, ea, publisher, sendEndpointProvider, cancellationToken);

    /// <summary>
    /// Creates a live typed <see cref="MessageContext{TMessage}"/> from the envelope-bearing receive path.
    /// Metadata comes from the envelope; transport-level fields (routing key, redelivery, retry count, reply-to fallback)
    /// come from <paramref name="ea"/>.
    /// </summary>
    public static MessageContext<TMessage> CreateContext<TMessage>(
        TMessage message,
        BasicDeliverEventArgs ea,
        MessageEnvelope envelope,
        IPublisher? publisher,
        ISendEndpointProvider? sendEndpointProvider,
        CancellationToken cancellationToken)
    {
        var transportHeaders = ea.BasicProperties.Headers ?? new Dictionary<string, object?>();
        return MessageContext.CreateFromEnvelope(
            message,
            envelope,
            ea.RoutingKey,
            ea.Redelivered,
            RabbitMqConstants.GetRetryCount(transportHeaders),
            ea.BasicProperties.ReplyTo,
            publisher,
            sendEndpointProvider,
            cancellationToken);
    }

    /// <summary>
    /// Builds a serializable <see cref="MessageContextSnapshot"/> of the delivery's transport metadata,
    /// used to capture the original context when producing a fault.
    /// </summary>
    public static MessageContextSnapshot CreateSnapshot(BasicDeliverEventArgs ea)
    {
        var metadata = ExtractMetadata(ea);
        return new MessageContextSnapshot
        {
            MessageId = metadata.MessageId,
            RequestId = metadata.RequestId,
            CorrelationId = metadata.CorrelationId,
            ConversationId = metadata.ConversationId,
            InitiatorId = metadata.InitiatorId,
            SourceAddress = metadata.SourceAddress,
            DestinationAddress = metadata.DestinationAddress,
            ResponseAddress = metadata.ResponseAddress,
            FaultAddress = metadata.FaultAddress,
            RoutingKey = metadata.RoutingKey,
            RetryCount = metadata.RetryCount,
        };
    }

    private static MessageContext<TMessage> BuildTypedMetadata<TMessage>(
        TMessage message,
        BasicDeliverEventArgs ea,
        IPublisher? publisher,
        ISendEndpointProvider? sendEndpointProvider,
        CancellationToken cancellationToken)
    {
        var metadata = ExtractMetadata(ea);
        return MessageContext.BuildTyped(
            message,
            publisher,
            sendEndpointProvider,
            metadata.MessageId,
            metadata.CorrelationId,
            metadata.RequestId,
            metadata.RoutingKey,
            metadata.Headers,
            metadata.Redelivered,
            metadata.RetryCount,
            metadata.ConversationId,
            metadata.InitiatorId,
            metadata.SourceAddress,
            metadata.DestinationAddress,
            metadata.ResponseAddress,
            metadata.FaultAddress,
            metadata.SentTime,
            metadata.ExpirationTime,
            cancellationToken);
    }

    /// <summary>
    /// Extracts the transport metadata common to both the fault snapshot and the bare-JSON typed context from a
    /// single AMQP delivery: the property/header paths, the response-address fallback to <c>ReplyTo</c>, and the
    /// TTL anchored to the delivery's timestamp.
    /// </summary>
    private static DeliveryMetadata ExtractMetadata(BasicDeliverEventArgs ea)
    {
        var props = ea.BasicProperties;
        var headers = props.Headers ?? new Dictionary<string, object?>();
        var sentTime = GetSentTime(props);
        return new DeliveryMetadata(
            MessageId: props.MessageId,
            CorrelationId: props.CorrelationId,
            RequestId: props.CorrelationId,
            RoutingKey: ea.RoutingKey,
            Headers: AmqpHeaderValueNormalizer.Normalize(headers),
            Redelivered: ea.Redelivered,
            RetryCount: RabbitMqConstants.GetRetryCount(headers),
            ConversationId: RabbitMqConstants.GetHeaderString(headers, "ConversationId"),
            InitiatorId: RabbitMqConstants.GetHeaderString(headers, "InitiatorId"),
            SourceAddress: RabbitMqConstants.GetHeaderUri(headers, "SourceAddress"),
            DestinationAddress: RabbitMqConstants.GetHeaderUri(headers, "DestinationAddress"),
            ResponseAddress: RabbitMqConstants.GetHeaderUri(headers, "ResponseAddress")
                ?? (string.IsNullOrEmpty(props.ReplyTo) ? null : new Uri($"queue:{props.ReplyTo}")),
            FaultAddress: RabbitMqConstants.GetHeaderUri(headers, "FaultAddress"),
            SentTime: sentTime,
            ExpirationTime: RabbitMqConstants.TryParseExpiration(props.Expiration, sentTime));
    }

    private static DateTimeOffset? GetSentTime(IReadOnlyBasicProperties props)
        => props.Timestamp.UnixTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(props.Timestamp.UnixTime) : null;

    private readonly record struct DeliveryMetadata(
        string? MessageId,
        string? CorrelationId,
        string? RequestId,
        string RoutingKey,
        IReadOnlyDictionary<string, object?> Headers,
        bool Redelivered,
        int RetryCount,
        string? ConversationId,
        string? InitiatorId,
        Uri? SourceAddress,
        Uri? DestinationAddress,
        Uri? ResponseAddress,
        Uri? FaultAddress,
        DateTimeOffset? SentTime,
        DateTimeOffset? ExpirationTime);
}
