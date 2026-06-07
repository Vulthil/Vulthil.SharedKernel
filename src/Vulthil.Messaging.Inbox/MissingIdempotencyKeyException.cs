namespace Vulthil.Messaging.Inbox;

/// <summary>
/// Thrown when an idempotency-guarded delivery has no resolvable idempotency key and
/// <see cref="InboxOptions.RejectMessagesWithoutKey"/> is enabled.
/// </summary>
public sealed class MissingIdempotencyKeyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MissingIdempotencyKeyException"/> class.
    /// </summary>
    public MissingIdempotencyKeyException()
        : base("The delivery has no resolvable idempotency key and cannot be deduplicated.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MissingIdempotencyKeyException"/> class with a specified message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public MissingIdempotencyKeyException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MissingIdempotencyKeyException"/> class with a specified message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public MissingIdempotencyKeyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MissingIdempotencyKeyException"/> class for the specified message type.
    /// </summary>
    /// <param name="messageType">The message type whose delivery lacked an idempotency key.</param>
    public MissingIdempotencyKeyException(Type messageType)
        : base($"A delivery of '{messageType?.FullName}' has no resolvable idempotency key and cannot be deduplicated. Set {nameof(InboxOptions)}.{nameof(InboxOptions.RejectMessagesWithoutKey)} to false to process such messages without deduplication, or supply a key selector.")
    {
        MessageType = messageType;
    }

    /// <summary>
    /// Gets the message type whose delivery lacked an idempotency key, when available.
    /// </summary>
    public Type? MessageType { get; }
}
