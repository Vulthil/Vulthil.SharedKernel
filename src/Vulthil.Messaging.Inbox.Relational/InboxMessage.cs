namespace Vulthil.Messaging.Inbox.Relational;

/// <summary>
/// A persisted idempotency marker recording that a message with a given key has been processed.
/// </summary>
public sealed record InboxMessage
{
    /// <summary>
    /// Gets the idempotency key of the processed message. This is the primary key and is unique per processed message.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Gets or sets the UTC timestamp at which the message was processed.
    /// </summary>
    public DateTimeOffset ProcessedOnUtc { get; set; }
}
