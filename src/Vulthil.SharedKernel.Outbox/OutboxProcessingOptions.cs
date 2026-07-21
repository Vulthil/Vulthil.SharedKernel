namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Configuration options for the outbox background processing service.
/// </summary>
public sealed class OutboxProcessingOptions
{
    /// <summary>
    /// Gets the base polling delay in seconds between outbox processing cycles. Default is 2.
    /// </summary>
    public int OutboxProcessingDelaySeconds { get; set; } = 2;
    /// <summary>
    /// Gets the maximum back-off delay in seconds when no messages are found. Default is 60.
    /// </summary>
    public int MaxDelaySeconds { get; set; } = 60;
    /// <summary>
    /// Gets the maximum number of outbox messages to process per cycle. Default is 10.
    /// </summary>
    public int BatchSize { get; set; } = 10;
    /// <summary>
    /// Gets the maximum number of delivery attempts before a message is dead-lettered — its <c>FailedOnUtc</c>
    /// is set and it is no longer relayed. Default is 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
    /// <summary>
    /// Gets a value indicating whether messages within a batch should be published in parallel.
    /// When enabled, each message is dispatched in its own dependency-injection scope so handlers do not share the
    /// relay's <c>DbContext</c>, and concurrency is bounded by <see cref="MaxDegreeOfParallelism"/>.
    /// </summary>
    public bool EnableParallelPublishing { get; set; }
    /// <summary>
    /// Gets the maximum number of messages dispatched concurrently when <see cref="EnableParallelPublishing"/> is
    /// enabled. Default is 4. Ignored when parallel publishing is disabled.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = 4;

    /// <summary>
    /// Gets or sets a value indicating whether Trace Identifiers should be included when publishing outbox messages.
    /// <br />
    /// This allows the originating action that persisted the outbox message to act as an owner for the resulting scope,
    /// even after the outbox delay
    /// <br/>
    /// Default: <see langword="true"/>
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether outbox relay metrics (the <c>vulthil.outbox.*</c> counters on the
    /// <see cref="Telemetry.MeterName"/> meter) are auto-registered with OpenTelemetry. Default: <see langword="true"/>.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Gets the retention sweep configuration. Set <see cref="OutboxRetentionOptions.Enabled"/> to periodically
    /// delete processed and dead-lettered rows older than <see cref="OutboxRetentionOptions.RetentionPeriod"/>.
    /// </summary>
    public OutboxRetentionOptions Retention { get; } = new();
}
