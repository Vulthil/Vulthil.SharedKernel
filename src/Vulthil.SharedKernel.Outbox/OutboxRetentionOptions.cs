namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Configuration for the outbox retention sweep, which periodically deletes processed rows once they are older than
/// <see cref="RetentionPeriod"/> and dead-lettered rows once they are older than <see cref="DeadLetterRetentionPeriod"/>,
/// so the outbox table does not grow unbounded.
/// </summary>
public sealed class OutboxRetentionOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the retention sweep runs. Default is <see langword="false"/>.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets how long a processed row is kept before it becomes eligible for deletion. A dead-lettered row is
    /// kept this long too, unless <see cref="DeadLetterRetentionPeriod"/> is set. Default is 7 days.
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets how long a dead-lettered row is kept before it becomes eligible for deletion. Default is
    /// <see langword="null"/>, which keeps a dead-lettered row for <see cref="RetentionPeriod"/>, like a processed row.
    /// </summary>
    /// <remarks>
    /// A dead-lettered row holds the only copy of a message the outbox gave up on: the error log names the message,
    /// but does not carry its content. Set a period longer than <see cref="RetentionPeriod"/> to keep the row available
    /// for inspection or replay after the <c>vulthil.outbox.dead_lettered</c> counter reports it, without keeping every
    /// processed row as long. <see cref="TimeSpan.MaxValue"/> keeps dead-lettered rows until you delete them.
    /// </remarks>
    public TimeSpan? DeadLetterRetentionPeriod { get; set; }

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
