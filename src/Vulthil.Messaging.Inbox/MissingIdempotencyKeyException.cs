namespace Vulthil.Messaging.Inbox;

/// <summary>
/// Thrown when an idempotency-guarded delivery has no resolvable idempotency key and
/// <see cref="InboxOptions.RejectMessagesWithoutKey"/> is enabled.
/// </summary>
public sealed class MissingIdempotencyKeyException : Exception
{
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
