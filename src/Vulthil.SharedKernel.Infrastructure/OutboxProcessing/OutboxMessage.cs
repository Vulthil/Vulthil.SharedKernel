namespace Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

/// <summary>
/// Represents a serialized domain event stored for reliable outbox-based delivery.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>
    /// Gets the unique identifier for this outbox message, generated as a version-7 UUID for natural ordering.
    /// </summary>
    public Guid Id { get; init; } = Guid.CreateVersion7();
    /// <summary>
    /// Gets the group identifier linking messages that were captured during the same <c>SaveChanges</c> call.
    /// </summary>
    public Guid GroupId { get; init; }
    /// <summary>
    /// Gets the fully-qualified type name of the serialized domain event.
    /// </summary>
    public required string Type { get; init; }
    /// <summary>
    /// Gets the JSON-serialized domain event content.
    /// </summary>
    public required string Content { get; init; }
    /// <summary>
    /// Gets the UTC timestamp when the domain event occurred.
    /// </summary>
    public DateTimeOffset OccurredOnUtc { get; init; }
    /// <summary>
    /// Gets or sets the UTC timestamp when the message was successfully processed, or <see langword="null"/> if pending.
    /// </summary>
    public DateTimeOffset? ProcessedOnUtc { get; set; }
    /// <summary>
    /// Gets or sets the number of delivery attempts made for this message.
    /// </summary>
    public int RetryCount { get; set; }
    /// <summary>
    /// Gets or sets the error message from the last failed delivery attempt, or <see langword="null"/> if no failures.
    /// </summary>
    public string? Error { get; set; }
    /// <summary>
    /// Activity TraceParent, for distributed tracing correlation.
    /// </summary>
    public string? TraceParent { get; set; }
    /// <summary>
    /// Activity TraceState, for distributed tracing correlation.
    /// </summary>
    public string? TraceState { get; set; }
}
