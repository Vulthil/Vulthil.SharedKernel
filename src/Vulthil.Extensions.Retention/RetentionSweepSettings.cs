namespace Vulthil.Extensions.Retention;

/// <summary>
/// How a retention sweep runs: a sweep starts on host start and then every <see cref="SweepInterval"/>, deletes
/// entries older than <see cref="RetentionPeriod"/>, and does so in batches of <see cref="BatchSize"/> until fewer
/// than a full batch remain. Callers validate these values on their own options before registering the sweep.
/// </summary>
public sealed class RetentionSweepSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetentionSweepSettings"/> class.
    /// </summary>
    /// <param name="retentionPeriod">How long an entry is kept before it becomes eligible for deletion.</param>
    /// <param name="sweepInterval">The delay between sweeps.</param>
    /// <param name="batchSize">The maximum number of entries deleted per batch within a sweep; values below 1 are treated as 1.</param>
    public RetentionSweepSettings(TimeSpan retentionPeriod, TimeSpan sweepInterval, int batchSize)
    {
        RetentionPeriod = retentionPeriod;
        SweepInterval = sweepInterval;
        BatchSize = batchSize;
    }

    /// <summary>Gets how long an entry is kept before it becomes eligible for deletion.</summary>
    public TimeSpan RetentionPeriod { get; }

    /// <summary>Gets the delay between sweeps.</summary>
    public TimeSpan SweepInterval { get; }

    /// <summary>Gets the maximum number of entries deleted per batch within a sweep; values below 1 are treated as 1.</summary>
    public int BatchSize { get; }
}
