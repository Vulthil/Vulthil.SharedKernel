using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.Transport;

/// <summary>
/// Transport-agnostic <see cref="IMessageContext"/> implementation describing a delivered message and the
/// transport services that back its <see cref="PublishAsync{TMessage}"/> and <see cref="SendAsync{TMessage}"/>
/// helpers. A transport builds one per delivery (typically via <see cref="CreateFromEnvelope{TMessage}"/>); a
/// context with no live transport binding (e.g. a fault snapshot) throws when a publish or send is attempted.
/// </summary>
public record MessageContext : IMessageContext
{
    /// <summary>Gets the publisher backing <see cref="PublishAsync{TMessage}"/>, or <see langword="null"/> for a snapshot context not bound to a live transport.</summary>
    public IPublisher? Publisher { get; init; }

    /// <summary>Gets the send endpoint provider backing <see cref="SendAsync{TMessage}"/>, or <see langword="null"/> for a snapshot context not bound to a live transport.</summary>
    public ISendEndpointProvider? SendEndpointProvider { get; init; }

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
    public required IReadOnlyDictionary<string, object?> Headers { get; init; }
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
        => (Publisher ?? throw SnapshotContextError()).PublishAsync(
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
        var provider = SendEndpointProvider ?? throw SnapshotContextError();
        var endpoint = await provider.GetSendEndpointAsync(destinationAddress, CancellationToken);
        await endpoint.SendAsync(
            message,
            ctx => PropagateAndConfigureAsync(ctx, configure),
            CancellationToken);
    }

    /// <summary>
    /// Builds a live typed context from a parsed <see cref="MessageEnvelope"/>. Transport-level fields the envelope
    /// does not carry are supplied by the caller: the broker <paramref name="routingKey"/>, the
    /// <paramref name="redelivered"/> flag, the in-memory <paramref name="retryCount"/>, and a
    /// <paramref name="replyToFallback"/> used for the response address when the envelope omits one.
    /// </summary>
    /// <typeparam name="TMessage">The deserialized message type.</typeparam>
    /// <param name="message">The deserialized message.</param>
    /// <param name="envelope">The parsed wire envelope supplying the message metadata.</param>
    /// <param name="routingKey">The broker routing key the message arrived on.</param>
    /// <param name="redelivered">Whether the broker flagged this as a redelivery.</param>
    /// <param name="retryCount">The current in-memory retry attempt.</param>
    /// <param name="replyToFallback">A reply destination to use when the envelope carries no response address, or <see langword="null"/>.</param>
    /// <param name="publisher">The publisher backing <see cref="PublishAsync{TMessage}"/>, or <see langword="null"/>.</param>
    /// <param name="sendEndpointProvider">The provider backing <see cref="SendAsync{TMessage}"/>, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token observed while consuming the message.</param>
    /// <returns>The constructed typed context.</returns>
    public static MessageContext<TMessage> CreateFromEnvelope<TMessage>(
        TMessage message,
        MessageEnvelope envelope,
        string routingKey,
        bool redelivered,
        int retryCount,
        string? replyToFallback,
        IPublisher? publisher,
        ISendEndpointProvider? sendEndpointProvider,
        CancellationToken cancellationToken)
    {
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
            RoutingKey = routingKey,
            Headers = userHeaders,
            Redelivered = redelivered,
            RetryCount = retryCount,
            ConversationId = envelope.ConversationId,
            InitiatorId = envelope.InitiatorId,
            SourceAddress = ParseAddress(envelope.SourceAddress),
            DestinationAddress = ParseAddress(envelope.DestinationAddress),
            ResponseAddress = ParseAddress(envelope.ResponseAddress)
                ?? (string.IsNullOrEmpty(replyToFallback) ? null : new Uri($"queue:{replyToFallback}")),
            FaultAddress = ParseAddress(envelope.FaultAddress),
            SentTime = envelope.SentTime,
            ExpirationTime = envelope.ExpirationTime,
        };
    }

    private async ValueTask PropagateAndConfigureAsync(IPublishContext ctx, Func<IPublishContext, ValueTask>? configure)
    {
        // 1. Auto-propagate correlation metadata from the incoming context first.
        if (!string.IsNullOrEmpty(CorrelationId))
        {
            ctx.SetCorrelationId(CorrelationId);
        }

        var conversationId = ConversationId ?? (string.IsNullOrEmpty(CorrelationId) ? null : CorrelationId);
        if (!string.IsNullOrEmpty(conversationId))
        {
            ctx.SetConversationId(conversationId);
        }

        if (!string.IsNullOrEmpty(MessageId))
        {
            ctx.SetInitiatorId(MessageId);
        }

        // 2. Caller's configure callback runs last so it can override any auto-set value.
        if (configure is not null)
        {
            await configure(ctx);
        }
    }

    private static InvalidOperationException SnapshotContextError() =>
        new("This message context is a snapshot (e.g. a fault envelope) and is not bound to a live transport.");

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

/// <summary>
/// A strongly-typed <see cref="MessageContext"/> carrying the deserialized message payload.
/// </summary>
/// <typeparam name="TMessage">The message type.</typeparam>
public sealed record MessageContext<TMessage> : MessageContext, IMessageContext<TMessage>
{
    /// <inheritdoc />
    public required TMessage Message { get; init; }
}
