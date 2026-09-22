using System.ComponentModel.DataAnnotations;

namespace Vulthil.Messaging.Inbox.EntityFrameworkCore;

/// <summary>
/// A persisted idempotency marker recording that a message with a given key has been processed. Shared by the
/// relational and Cosmos idempotency stores.
/// </summary>
/// <remarks>
/// The entity is valid by convention alone: <see cref="MessageId"/> is annotated as the bounded primary key, so a
/// context that exposes the set without calling a store package's <c>Apply*Inbox</c> extension still builds a
/// working model. The extensions add the provider-specific layout on top (for Cosmos, the dedicated container and
/// partition key).
/// </remarks>
public sealed record InboxMessage
{
    /// <summary>
    /// Gets the idempotency key of the processed message. This is the primary key and is unique per processed message.
    /// </summary>
    [Key]
    [MaxLength(256)]
    public required string MessageId { get; init; }

    /// <summary>
    /// Gets the UTC timestamp at which the message was processed.
    /// </summary>
    public DateTimeOffset ProcessedOnUtc { get; init; }
}
