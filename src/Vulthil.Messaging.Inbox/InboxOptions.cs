namespace Vulthil.Messaging.Inbox;

/// <summary>
/// Configuration options for the idempotent-receiver (inbox) consume filter.
/// </summary>
public sealed class InboxOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether a delivery with no resolvable idempotency key is rejected.
    /// When <see langword="true"/> (the default), such a delivery throws <see cref="MissingIdempotencyKeyException"/>
    /// so it cannot bypass the guard silently. When <see langword="false"/>, the delivery is processed without
    /// deduplication.
    /// </summary>
    public bool RejectMessagesWithoutKey { get; set; } = true;

    /// <summary>
    /// Gets the retention sweep configuration. Set <see cref="InboxRetentionOptions.Enabled"/> to periodically
    /// delete idempotency markers older than <see cref="InboxRetentionOptions.RetentionPeriod"/>.
    /// </summary>
    public InboxRetentionOptions Retention { get; } = new();
}
