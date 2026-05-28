using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.RabbitMq.Envelope;
using Vulthil.Messaging.RabbitMq.Sending;

namespace Vulthil.Messaging.RabbitMq.Consumers;

internal record MessageContext : IMessageContext
{
    /// <summary>The publisher backing <see cref="PublishAsync"/>. Defaults to <see cref="NullPublisher.Instance"/> for snapshots.</summary>
    public required IPublisher Publisher { get; init; }

    /// <summary>The send endpoint provider backing <see cref="SendAsync"/>. Defaults to <see cref="NullSendEndpointProvider.Instance"/> for snapshots.</summary>
    public required ISendEndpointProvider SendEndpointProvider { get; init; }

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
            ctx => PropagateAndConfigureAsync(ctx, configure),
            CancellationToken);

    /// <inheritdoc />
    public async Task SendAsync<TMessage>(
        Uri destinationAddress,
        TMessage message,
        Func<IPublishContext, ValueTask>? configure = null)
        where TMessage : notnull
    {
        ArgumentNullException.ThrowIfNull(destinationAddress);
        var endpoint = await SendEndpointProvider.GetSendEndpointAsync(destinationAddress, CancellationToken);
        await endpoint.SendAsync(
            message,
            ctx => PropagateAndConfigureAsync(ctx, configure),
            CancellationToken);
    }

    private async ValueTask PropagateAndConfigureAsync(IPublishContext ctx, Func<IPublishContext, ValueTask>? configure)
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
    }

    /// <summary>
    /// Creates a snapshot <see cref="MessageContext"/> with no live transport binding.
    /// Used by fault publishing to capture the original context for serialization.
    /// </summary>
    public static MessageContext CreateContext(BasicDeliverEventArgs ea) =>
        BuildMetadata(ea, NullPublisher.Instance, NullSendEndpointProvider.Instance, cancellationToken: default);

    /// <summary>
    /// Creates a live <see cref="MessageContext"/> bound to the specified transport services and cancellation token.
    /// </summary>
    public static MessageContext CreateContext(
        BasicDeliverEventArgs ea,
        IPublisher publisher,
        ISendEndpointProvider sendEndpointProvider,
        CancellationToken cancellationToken) =>
        BuildMetadata(ea, publisher, sendEndpointProvider, cancellationToken);

    /// <summary>
    /// Creates a snapshot typed <see cref="MessageContext{TMessage}"/> with no live transport binding.
    /// </summary>
    public static MessageContext<TMessage> CreateContext<TMessage>(TMessage message, BasicDeliverEventArgs ea) =>
        BuildTypedMetadata(message, ea, NullPublisher.Instance, NullSendEndpointProvider.Instance, cancellationToken: default);

    /// <summary>
    /// Creates a live typed <see cref="MessageContext{TMessage}"/> bound to the specified transport services and cancellation token.
    /// Used by the bare-JSON receive path; metadata comes from <paramref name="ea"/> and its <c>BasicProperties</c> headers.
    /// </summary>
    public static MessageContext<TMessage> CreateContext<TMessage>(
        TMessage message,
        BasicDeliverEventArgs ea,
        IPublisher publisher,
        ISendEndpointProvider sendEndpointProvider,
        CancellationToken cancellationToken) =>
        BuildTypedMetadata(message, ea, publisher, sendEndpointProvider, cancellationToken);

    /// <summary>
    /// Creates a live typed <see cref="MessageContext{TMessage}"/> from the envelope-bearing receive path.
    /// Metadata comes from the envelope; transport-level fields (RoutingKey, Redelivered, retry count) still come from <paramref name="ea"/>.
    /// </summary>
    public static MessageContext<TMessage> CreateContext<TMessage>(
        TMessage message,
        BasicDeliverEventArgs ea,
        MessageEnvelope envelope,
        IPublisher publisher,
        ISendEndpointProvider sendEndpointProvider,
        CancellationToken cancellationToken) =>
        BuildTypedMetadataFromEnvelope(message, ea, envelope, publisher, sendEndpointProvider, cancellationToken);

    private static MessageContext BuildMetadata(
        BasicDeliverEventArgs ea,
        IPublisher publisher,
        ISendEndpointProvider sendEndpointProvider,
        CancellationToken cancellationToken)
    {
        var props = ea.BasicProperties;
        var headers = props.Headers ?? new Dictionary<string, object?>();
        return new MessageContext
        {
            Publisher = publisher,
            SendEndpointProvider = sendEndpointProvider,
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
        ISendEndpointProvider sendEndpointProvider,
        CancellationToken cancellationToken)
    {
        var props = ea.BasicProperties;
        var headers = props.Headers ?? new Dictionary<string, object?>();
        return new MessageContext<TMessage>
        {
            Message = message,
            Publisher = publisher,
            SendEndpointProvider = sendEndpointProvider,
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

    private static MessageContext<TMessage> BuildTypedMetadataFromEnvelope<TMessage>(
        TMessage message,
        BasicDeliverEventArgs ea,
        MessageEnvelope envelope,
        IPublisher publisher,
        ISendEndpointProvider sendEndpointProvider,
        CancellationToken cancellationToken)
    {
        var props = ea.BasicProperties;
        var transportHeaders = props.Headers ?? new Dictionary<string, object?>();
        var userHeaders = envelope.Headers is { } h ? new Dictionary<string, object?>(h) : [];

        return new MessageContext<TMessage>
        {
            Message = message,
            Publisher = publisher,
            SendEndpointProvider = sendEndpointProvider,
            CancellationToken = cancellationToken,
            MessageId = envelope.MessageId,
            CorrelationId = envelope.CorrelationId ?? string.Empty,
            RequestId = envelope.RequestId ?? envelope.CorrelationId,
            RoutingKey = ea.RoutingKey,
            Headers = userHeaders,
            Redelivered = ea.Redelivered,
            RetryCount = RabbitMqConstants.GetRetryCount(transportHeaders),
            ConversationId = envelope.ConversationId,
            InitiatorId = envelope.InitiatorId,
            SourceAddress = ParseAddress(envelope.SourceAddress),
            DestinationAddress = ParseAddress(envelope.DestinationAddress),
            ResponseAddress = ParseAddress(envelope.ResponseAddress)
                ?? (string.IsNullOrEmpty(props.ReplyTo) ? null : new Uri($"queue:{props.ReplyTo}")),
            FaultAddress = ParseAddress(envelope.FaultAddress),
            SentTime = envelope.SentTime,
            ExpirationTime = envelope.ExpirationTime,
        };
    }

    private static Uri? ParseAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri
            : new Uri($"queue:{value}");
    }
}

internal sealed record MessageContext<TMessage> : MessageContext, IMessageContext<TMessage>
{
    /// <inheritdoc />
    public required TMessage Message { get; init; }
}
