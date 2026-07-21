namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Configuration for the outbox retention sweep, which periodically deletes processed and dead-lettered outbox rows
/// once they are older than <see cref="RetentionPeriod"/> so the outbox table does not grow unbounded.
/// </summary>
public sealed class OutboxRetentionOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the retention sweep runs. Default is <see langword="false"/>.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets how long a processed or dead-lettered row is kept before it becomes eligible for deletion.
    /// Default is 7 days.
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets the delay between retention sweeps. Default is 1 hour.
    /// </summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the maximum number of rows deleted per batch within a sweep; a sweep keeps deleting batches until
    /// fewer than this many rows remain. Default is 1000.
    /// </summary>
    public int BatchSize { get; set; } = 1000;
}
