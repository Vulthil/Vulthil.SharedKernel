using System.ComponentModel.DataAnnotations;

namespace Vulthil.Messaging.Inbox;

/// <summary>
/// Configuration for the inbox retention sweep, which periodically deletes idempotency markers once they are older
/// than <see cref="RetentionPeriod"/> so the inbox table does not grow unbounded.
/// </summary>
public sealed class InboxRetentionOptions
{
    /// <summary>
    /// Gets or sets how long a marker is kept before it becomes eligible for deletion. Choose a value comfortably
    /// longer than the broker's maximum redelivery delay so a marker is never removed while a duplicate could still
    /// arrive. Default is 7 days.
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the delay between retention sweeps. Default is 1 hour.
    /// </summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the maximum number of markers deleted per batch within a sweep; a sweep keeps deleting batches
    /// until fewer than this many markers remain. Default is 1000.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int BatchSize { get; set; } = 1000;
}
