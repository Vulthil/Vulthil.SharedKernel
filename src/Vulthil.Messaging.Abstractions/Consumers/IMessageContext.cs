using Vulthil.Messaging.Abstractions.Publishers;

namespace Vulthil.Messaging.Abstractions.Consumers;

/// <summary>
/// Provides transport-level metadata for a received message.
/// </summary>
public interface IMessageContext
{
    // --- Identity & Correlation ---
    /// <summary>
    /// Gets the unique message identifier assigned by the transport, or <see langword="null"/> if not set.
    /// </summary>
    string? MessageId { get; }
    /// <summary>
    /// Gets the request identifier used to correlate a reply back to the original request, or <see langword="null"/> if not set.
    /// </summary>
    string? RequestId { get; }
    /// <summary>
    /// Gets the conversation identifier that groups related messages across services, or <see langword="null"/> if not set.
    /// </summary>
    string? ConversationId { get; }
    /// <summary>
    /// Gets the correlation identifier linking this message to a business transaction, or <see langword="null"/> if not set.
    /// </summary>
    string? CorrelationId { get; }
    /// <summary>
    /// Gets the identifier of the message that initiated this chain, or <see langword="null"/> if not set.
    /// </summary>
    string? InitiatorId { get; }

    // --- Addressing ---
    /// <summary>
    /// Gets the address of the endpoint that sent the message, or <see langword="null"/> if unknown.
    /// </summary>
    Uri? SourceAddress { get; }
    /// <summary>
    /// Gets the intended destination address for the message, or <see langword="null"/> if not set.
    /// </summary>
    Uri? DestinationAddress { get; }
    /// <summary>
    /// Gets the address where replies should be sent, or <see langword="null"/> if no reply is expected.
    /// </summary>
    Uri? ResponseAddress { get; }
    /// <summary>
    /// Gets the address where fault notifications should be sent, or <see langword="null"/> to use the default.
    /// </summary>
    Uri? FaultAddress { get; }

    // --- Transport Details ---
    /// <summary>
    /// Gets the routing key that was used by the transport to deliver this message.
    /// </summary>
    string RoutingKey { get; }
    /// <summary>
    /// Gets the transport headers associated with the message, containing custom metadata.
    /// </summary>
    /// <remarks>
    /// Values are normalized when the context is built, so every consume path (envelope, bare AMQP, outbox
    /// relay) surfaces the same CLR primitives: strings arrive as <see cref="string"/>, booleans as
    /// <see cref="bool"/>, and numbers as <see cref="int"/>, <see cref="long"/>, or <see cref="double"/> —
    /// the narrowest of the three that represents the value. Header values published as non-primitive
    /// objects have no CLR type on the wire and surface in their JSON form: as a
    /// <see cref="System.Text.Json.JsonElement"/> when published as an object or array, or as a
    /// <see cref="string"/> for types JSON represents as strings (e.g. <see cref="Guid"/>,
    /// <see cref="DateTimeOffset"/>).
    /// </remarks>
    IReadOnlyDictionary<string, object?> Headers { get; }

    // --- Timing & Lifecycle ---
    /// <summary>
    /// Gets the UTC timestamp when the message was originally sent, or <see langword="null"/> if not recorded.
    /// </summary>
    DateTimeOffset? SentTime { get; }
    /// <summary>
    /// Gets the UTC timestamp after which the message should be discarded, or <see langword="null"/> if it does not expire.
    /// </summary>
    DateTimeOffset? ExpirationTime { get; }

    // --- Retry Metadata ---
    /// <summary>
    /// Gets the number of times this message has been retried. A value of 0 indicates the first delivery attempt.
    /// </summary>
    int RetryCount { get; }
    /// <summary>
    /// Gets a value indicating whether the broker redelivered this message (e.g., after a consumer crash).
    /// </summary>
    bool Redelivered { get; }

    // --- Consumer Capabilities ---
    /// <summary>
    /// Gets the cancellation token associated with the current delivery. Combines the transport's stop signal
    /// with the consumer scope, allowing handlers to observe both.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Publishes a message via the underlying transport, automatically propagating correlation metadata
    /// (<c>CorrelationId</c>, <c>ConversationId</c>, <c>InitiatorId</c>) from the incoming context to the outgoing message.
    /// The caller-supplied <paramref name="configure"/> callback runs after propagation, so it can override any auto-set value.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to publish.</typeparam>
    /// <param name="message">The message to publish.</param>
    /// <param name="configure">Optional callback for customizing the outgoing publish context.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    Task PublishAsync<TMessage>(
        TMessage message,
        Func<IPublishContext, ValueTask>? configure = null)
        where TMessage : notnull;

    /// <summary>
    /// Sends a message point-to-point to the specified destination, automatically propagating correlation metadata
    /// (<c>CorrelationId</c>, <c>ConversationId</c>, <c>InitiatorId</c>) from the incoming context to the outgoing message.
    /// The caller-supplied <paramref name="configure"/> callback runs after propagation, so it can override any auto-set value.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to send.</typeparam>
    /// <param name="destinationAddress">The destination endpoint address (e.g. <c>queue:order-commands</c>).</param>
    /// <param name="message">The message to send.</param>
    /// <param name="configure">Optional callback for customizing the outgoing publish context.</param>
    /// <returns>A task representing the asynchronous send operation.</returns>
    Task SendAsync<TMessage>(
        Uri destinationAddress,
        TMessage message,
        Func<IPublishContext, ValueTask>? configure = null)
        where TMessage : notnull;
}

/// <summary>
/// Provides transport-level metadata and the deserialized payload for a received message.
/// </summary>
/// <typeparam name="TMessage">The type of message payload.</typeparam>
public interface IMessageContext<out TMessage> : IMessageContext
{
    /// <summary>
    /// Gets the deserialized message payload.
    /// </summary>
    TMessage Message { get; }
}
