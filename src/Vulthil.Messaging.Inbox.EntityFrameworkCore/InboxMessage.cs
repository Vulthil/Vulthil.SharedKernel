namespace Vulthil.Messaging.Inbox.EntityFrameworkCore;

/// <summary>
/// A persisted idempotency marker recording that a message with a given key has been processed. Shared by the
/// relational and Cosmos idempotency stores.
/// </summary>
public sealed record InboxMessage
{
    /// <summary>
    /// Gets the idempotency key of the processed message. This is the primary key and is unique per processed message.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Gets the UTC timestamp at which the message was processed.
    /// </summary>
    public DateTimeOffset ProcessedOnUtc { get; init; }
}
