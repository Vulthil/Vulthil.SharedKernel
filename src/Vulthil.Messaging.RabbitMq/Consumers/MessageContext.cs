using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.RabbitMq.Consumers;

internal record MessageContext : IMessageContext
{
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public string? MessageId { get; init; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public required string CorrelationId { get; init; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public string? RequestId { get; init; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public required string RoutingKey { get; init; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public required IDictionary<string, object?> Headers { get; init; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public int RetryCount { get; init; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public bool Redelivered { get; init; }

    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public string? ConversationId { get; init; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public string? InitiatorId { get; init; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public Uri? SourceAddress { get; init; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public Uri? DestinationAddress { get; init; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public Uri? ResponseAddress { get; init; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public Uri? FaultAddress { get; init; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public DateTimeOffset? SentTime { get; init; }
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public DateTimeOffset? ExpirationTime { get; init; }

    /// <summary>
    /// Executes this member.
    /// </summary>
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

    /// <summary>
    /// Executes this member.
    /// </summary>
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
    /// <summary>
    /// Gets or sets this member value.
    /// </summary>
    public required TMessage Message { get; init; }
}

