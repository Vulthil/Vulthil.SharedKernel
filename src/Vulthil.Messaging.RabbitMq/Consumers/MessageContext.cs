using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.RabbitMq.Consumers;

internal record MessageContext : IMessageContext
{
    public string? MessageId { get; init; }
    public required string CorrelationId { get; init; }
    public string? RequestId { get; init; }
    public required string RoutingKey { get; init; }
    public required IDictionary<string, object?> Headers { get; init; }
    public int RetryCount { get; init; }
    public bool Redelivered { get; init; }

    public string? ConversationId { get; init; }
    public string? InitiatorId { get; init; }
    public Uri? SourceAddress { get; init; }
    public Uri? DestinationAddress { get; init; }
    public Uri? ResponseAddress { get; init; }
    public Uri? FaultAddress { get; init; }
    public DateTimeOffset? SentTime { get; init; }
    public DateTimeOffset? ExpirationTime { get; init; }

    public static MessageContext CreateContext(BasicDeliverEventArgs ea)
    {
        var props = ea.BasicProperties;
        var headers = props.Headers ?? new Dictionary<string, object?>();
        return new MessageContext
        {
            MessageId = props.MessageId,
            CorrelationId = props.CorrelationId ?? string.Empty,
            RequestId = props.CorrelationId,
            RoutingKey = ea.RoutingKey,
            Headers = headers,
            Redelivered = ea.Redelivered,
            RetryCount = RabbitMqConstants.GetRetryCount(headers),

            // Custom Header Mapping
            ConversationId = RabbitMqConstants.GetHeaderString(headers, "ConversationId"),
            InitiatorId = RabbitMqConstants.GetHeaderString(headers, "InitiatorId"),
            SourceAddress = RabbitMqConstants.GetHeaderUri(headers, "SourceAddress"),
            DestinationAddress = RabbitMqConstants.GetHeaderUri(headers, "DestinationAddress"),
            ResponseAddress = RabbitMqConstants.GetHeaderUri(headers, "ResponseAddress")
                ?? (string.IsNullOrEmpty(props.ReplyTo) ? null : new Uri($"queue:{props.ReplyTo}")),
            FaultAddress = RabbitMqConstants.GetHeaderUri(headers, "FaultAddress"),

            // Timing
            SentTime = props.Timestamp.UnixTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(props.Timestamp.UnixTime) : null,
            // Expiration can come from the header or the property
            ExpirationTime = RabbitMqConstants.TryParseExpiration(props.Expiration)
        };
    }

    public static MessageContext<TMessage> CreateContext<TMessage>(
        TMessage message,
        BasicDeliverEventArgs ea)
    {
        var props = ea.BasicProperties;
        var headers = props.Headers ?? new Dictionary<string, object?>();

        return new MessageContext<TMessage>
        {
            Message = message,
            MessageId = props.MessageId,
            CorrelationId = props.CorrelationId ?? string.Empty,
            RequestId = props.CorrelationId,
            RoutingKey = ea.RoutingKey,
            Headers = headers,
            Redelivered = ea.Redelivered,
            RetryCount = RabbitMqConstants.GetRetryCount(headers),

            // Custom Header Mapping
            ConversationId = RabbitMqConstants.GetHeaderString(headers, "ConversationId"),
            InitiatorId = RabbitMqConstants.GetHeaderString(headers, "InitiatorId"),
            SourceAddress = RabbitMqConstants.GetHeaderUri(headers, "SourceAddress"),
            DestinationAddress = RabbitMqConstants.GetHeaderUri(headers, "DestinationAddress"),
            ResponseAddress = RabbitMqConstants.GetHeaderUri(headers, "ResponseAddress")
                ?? (string.IsNullOrEmpty(props.ReplyTo) ? null : new Uri($"queue:{props.ReplyTo}")),
            FaultAddress = RabbitMqConstants.GetHeaderUri(headers, "FaultAddress"),

            // Timing
            SentTime = props.Timestamp.UnixTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(props.Timestamp.UnixTime) : null,
            // Expiration can come from the header or the property
            ExpirationTime = RabbitMqConstants.TryParseExpiration(props.Expiration)
        };
    }
}
internal sealed record MessageContext<TMessage> : MessageContext, IMessageContext<TMessage>
{
    public required TMessage Message { get; init; }
}

