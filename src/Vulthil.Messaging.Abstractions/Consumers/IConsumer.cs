namespace Vulthil.Messaging.Abstractions.Consumers;

/// <summary>
/// Marker interface for message consumers.
/// </summary>
public interface IConsumer;
/// <summary>
/// Defines a consumer that processes messages of type <typeparamref name="TMessage"/>.
/// </summary>
/// <typeparam name="TMessage">The type of message to consume.</typeparam>
public interface IConsumer<in TMessage> : IConsumer
{
    /// <summary>
    /// Processes the received message.
    /// </summary>
    /// <param name="messageContext">The message context containing the payload and metadata.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ConsumeAsync(IMessageContext<TMessage> messageContext, CancellationToken cancellationToken = default);
}

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
    IDictionary<string, object?> Headers { get; }

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
/// <summary>
/// Represents a fault envelope containing the original message and failure details.
/// </summary>
/// <typeparam name="TMessage">The type of the original message.</typeparam>
public record Fault<TMessage> where TMessage : notnull
{
    /// <summary>
    /// Gets the original message that caused the fault.
    /// </summary>
    public required TMessage Message { get; init; }
    /// <summary>
    /// Gets the exception message describing the failure.
    /// </summary>
    public required string ExceptionMessage { get; init; }
    /// <summary>
    /// Gets the stack trace of the exception, or <see langword="null"/> if unavailable.
    /// </summary>
    public required string? StackTrace { get; init; }
    /// <summary>
    /// Gets the fully-qualified type name of the exception.
    /// </summary>
    public required string ExceptionType { get; init; }
    /// <summary>
    /// Gets the UTC timestamp when the fault occurred.
    /// </summary>
    public required DateTimeOffset FaultedAt { get; init; }
    /// <summary>
    /// Gets the original message context at the time of the fault.
    /// </summary>
    public required IMessageContext OriginalContext { get; init; }
}
