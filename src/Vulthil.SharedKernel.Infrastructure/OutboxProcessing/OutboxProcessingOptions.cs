using System.ComponentModel.DataAnnotations;

namespace Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

/// <summary>
/// Configuration options for the outbox background processing service.
/// </summary>
public sealed class OutboxProcessingOptions
{
    /// <summary>
    /// Gets the base polling delay in seconds between outbox processing cycles. Default is 2.
    /// </summary>
    [Range(1, 100)]
    public int OutboxProcessingDelayInSeconds { get; init; } = 2;
    /// <summary>
    /// Gets the maximum back-off delay in seconds when no messages are found. Default is 60.
    /// </summary>
    [Range(1, 300)]
    public int MaxDelaySeconds { get; init; } = 60;
    /// <summary>
    /// Gets the maximum number of outbox messages to process per cycle. Default is 10.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int BatchSize { get; init; } = 10;
    /// <summary>
    /// Gets the maximum number of delivery attempts before a message is marked as processed. Default is 3.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxRetries { get; init; } = 3;
    /// <summary>
    /// Gets a value indicating whether messages within a batch should be published in parallel.
    /// </summary>
    public bool EnableParallelPublishing { get; init; }
}
