namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Represents a serialized domain event stored for reliable outbox-based delivery.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>
    /// Gets the unique identifier for this outbox message, generated as a version-7 UUID so the relay can use it as a
    /// stable, time-ordered tie-breaker when ordering messages with the same <see cref="OccurredOnUtc"/>.
    /// </summary>
    public Guid Id { get; init; } = Guid.CreateVersion7();
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
    /// Gets or sets the UTC timestamp when the message was successfully processed, or <see langword="null"/> if pending
    /// or dead-lettered.
    /// </summary>
    public DateTimeOffset? ProcessedOnUtc { get; set; }
    /// <summary>
    /// Gets or sets the UTC timestamp when the message was dead-lettered after exhausting its retries, or
    /// <see langword="null"/> if it has not permanently failed. A dead-lettered message is never relayed again and is
    /// distinct from a successfully processed one (<see cref="ProcessedOnUtc"/>); inspect <see cref="Error"/> for the
    /// last failure.
    /// </summary>
    public DateTimeOffset? FailedOnUtc { get; set; }
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
    /// <summary>
    /// Gets the sink this message is relayed to (in-process domain event, broker publish, or broker send). Defaults
    /// to <see cref="OutboxDestination.DomainEvent"/>.
    /// </summary>
    public OutboxDestination Destination { get; init; }
    /// <summary>
    /// Gets optional destination-specific metadata serialized as JSON (e.g. the broker message id, correlation,
    /// headers, and destination address for bus-publish rows). <see langword="null"/> for domain-event rows.
    /// </summary>
    public string? Metadata { get; init; }
}
