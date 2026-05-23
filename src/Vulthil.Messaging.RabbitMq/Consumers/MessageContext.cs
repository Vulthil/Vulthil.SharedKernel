using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.RabbitMq.Consumers;

internal record MessageContext : IMessageContext
{
    /// <summary>The publisher backing <see cref="PublishAsync"/>. Defaults to <see cref="NullPublisher.Instance"/> for snapshots.</summary>
    public required IPublisher Publisher { get; init; }

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; init; }

    /// <inheritdoc />
    public string? MessageId { get; init; }
    /// <inheritdoc />
    public required string CorrelationId { get; init; }
    /// <inheritdoc />
    public string? RequestId { get; init; }
    /// <inheritdoc />
    public required string RoutingKey { get; init; }
    /// <inheritdoc />
    public required IDictionary<string, object?> Headers { get; init; }
    /// <inheritdoc />
    public int RetryCount { get; init; }
    /// <inheritdoc />
    public bool Redelivered { get; init; }
    /// <inheritdoc />
    public string? ConversationId { get; init; }
    /// <inheritdoc />
    public string? InitiatorId { get; init; }
    /// <inheritdoc />
    public Uri? SourceAddress { get; init; }
    /// <inheritdoc />
    public Uri? DestinationAddress { get; init; }
    /// <inheritdoc />
    public Uri? ResponseAddress { get; init; }
    /// <inheritdoc />
    public Uri? FaultAddress { get; init; }
    /// <inheritdoc />
    public DateTimeOffset? SentTime { get; init; }
    /// <inheritdoc />
    public DateTimeOffset? ExpirationTime { get; init; }

    /// <inheritdoc />
    public Task PublishAsync<TMessage>(TMessage message, Func<IPublishContext, ValueTask>? configure = null)
        where TMessage : notnull
        => Publisher.PublishAsync(
            message,
            async ctx =>
            {
                // 1. Auto-propagate correlation metadata from the incoming context first.
                if (!string.IsNullOrEmpty(CorrelationId))
                {
                    ctx.SetCorrelationId(CorrelationId);
                }
                ctx.ConversationId = ConversationId ?? (string.IsNullOrEmpty(CorrelationId) ? null : CorrelationId);
                ctx.InitiatorId = MessageId;

                // 2. Caller's configure callback runs last so it can override any auto-set value.
                if (configure is not null)
                {
                    await configure(ctx);
                }
            },
            CancellationToken);

    /// <summary>
    /// Creates a snapshot <see cref="MessageContext"/> with no live publisher binding.
    /// Used by fault publishing to capture the original context for serialization.
    /// </summary>
    public static MessageContext CreateContext(BasicDeliverEventArgs ea) =>
        BuildMetadata(ea, NullPublisher.Instance, cancellationToken: default);

    /// <summary>
    /// Creates a live <see cref="MessageContext"/> bound to the specified publisher and cancellation token.
    /// </summary>
    public static MessageContext CreateContext(BasicDeliverEventArgs ea, IPublisher publisher, CancellationToken cancellationToken) =>
        BuildMetadata(ea, publisher, cancellationToken);

    /// <summary>
    /// Creates a snapshot typed <see cref="MessageContext{TMessage}"/> with no live publisher binding.
    /// </summary>
    public static MessageContext<TMessage> CreateContext<TMessage>(TMessage message, BasicDeliverEventArgs ea) =>
        BuildTypedMetadata(message, ea, NullPublisher.Instance, cancellationToken: default);

    /// <summary>
    /// Creates a live typed <see cref="MessageContext{TMessage}"/> bound to the specified publisher and cancellation token.
    /// </summary>
    public static MessageContext<TMessage> CreateContext<TMessage>(
        TMessage message,
        BasicDeliverEventArgs ea,
        IPublisher publisher,
        CancellationToken cancellationToken) =>
        BuildTypedMetadata(message, ea, publisher, cancellationToken);

    private static MessageContext BuildMetadata(BasicDeliverEventArgs ea, IPublisher publisher, CancellationToken cancellationToken)
    {
        var props = ea.BasicProperties;
        var headers = props.Headers ?? new Dictionary<string, object?>();
        return new MessageContext
        {
            Publisher = publisher,
            CancellationToken = cancellationToken,
            MessageId = props.MessageId,
            CorrelationId = props.CorrelationId ?? string.Empty,
            RequestId = props.CorrelationId,
            RoutingKey = ea.RoutingKey,
            Headers = headers,
            Redelivered = ea.Redelivered,
            RetryCount = RabbitMqConstants.GetRetryCount(headers),
            ConversationId = RabbitMqConstants.GetHeaderString(headers, "ConversationId"),
            InitiatorId = RabbitMqConstants.GetHeaderString(headers, "InitiatorId"),
            SourceAddress = RabbitMqConstants.GetHeaderUri(headers, "SourceAddress"),
            DestinationAddress = RabbitMqConstants.GetHeaderUri(headers, "DestinationAddress"),
            ResponseAddress = RabbitMqConstants.GetHeaderUri(headers, "ResponseAddress")
                ?? (string.IsNullOrEmpty(props.ReplyTo) ? null : new Uri($"queue:{props.ReplyTo}")),
            FaultAddress = RabbitMqConstants.GetHeaderUri(headers, "FaultAddress"),
            SentTime = props.Timestamp.UnixTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(props.Timestamp.UnixTime) : null,
            ExpirationTime = RabbitMqConstants.TryParseExpiration(props.Expiration)
        };
    }

    private static MessageContext<TMessage> BuildTypedMetadata<TMessage>(
        TMessage message,
        BasicDeliverEventArgs ea,
        IPublisher publisher,
        CancellationToken cancellationToken)
    {
        var props = ea.BasicProperties;
        var headers = props.Headers ?? new Dictionary<string, object?>();
        return new MessageContext<TMessage>
        {
            Message = message,
            Publisher = publisher,
            CancellationToken = cancellationToken,
            MessageId = props.MessageId,
            CorrelationId = props.CorrelationId ?? string.Empty,
            RequestId = props.CorrelationId,
            RoutingKey = ea.RoutingKey,
            Headers = headers,
            Redelivered = ea.Redelivered,
            RetryCount = RabbitMqConstants.GetRetryCount(headers),
            ConversationId = RabbitMqConstants.GetHeaderString(headers, "ConversationId"),
            InitiatorId = RabbitMqConstants.GetHeaderString(headers, "InitiatorId"),
            SourceAddress = RabbitMqConstants.GetHeaderUri(headers, "SourceAddress"),
            DestinationAddress = RabbitMqConstants.GetHeaderUri(headers, "DestinationAddress"),
            ResponseAddress = RabbitMqConstants.GetHeaderUri(headers, "ResponseAddress")
                ?? (string.IsNullOrEmpty(props.ReplyTo) ? null : new Uri($"queue:{props.ReplyTo}")),
            FaultAddress = RabbitMqConstants.GetHeaderUri(headers, "FaultAddress"),
            SentTime = props.Timestamp.UnixTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(props.Timestamp.UnixTime) : null,
            ExpirationTime = RabbitMqConstants.TryParseExpiration(props.Expiration)
        };
    }
}

internal sealed record MessageContext<TMessage> : MessageContext, IMessageContext<TMessage>
{
    /// <inheritdoc />
    public required TMessage Message { get; init; }
}
