namespace Vulthil.Messaging.Queues;

/// <summary>
/// Fluent builder for constructing a <see cref="RetryPolicyDefinition"/>.
/// </summary>
public sealed class RetryPolicyConfigurator
{
    private readonly HashSet<Type> _ignoredExceptions = [];
    private List<TimeSpan> _intervals = [];
    /// <summary>
    /// Gets the maximum number of retry attempts configured via one of the interval methods.
    /// </summary>
    public int RetryLimit { get; private set; }
    private double _jitterFactor;
    private bool _inMemory;
    /// <summary>
    /// Gets the configured delay intervals between successive retry attempts.
    /// </summary>
    public IReadOnlyList<TimeSpan> Intervals => _intervals;
    /// <summary>
    /// Gets the exception types that should be excluded from retry attempts and immediately surfaced.
    /// </summary>
    public IReadOnlySet<Type> IgnoredExceptions => _ignoredExceptions;

    /// <summary>
    /// Configures immediate retries with zero delay.
    /// </summary>
    /// <param name="retryCount">The number of retries.</param>
    /// <returns>The same configurator, for chaining.</returns>
    public RetryPolicyConfigurator Immediate(int retryCount)
    {
        RetryLimit = retryCount;
        _intervals = [.. Enumerable.Repeat(TimeSpan.Zero, retryCount)];
        return this;
    }

    /// <summary>
    /// Configures explicit retry intervals.
    /// </summary>
    /// <param name="intervals">The delay between each retry attempt.</param>
    /// <returns>The same configurator, for chaining.</returns>
    public RetryPolicyConfigurator SetIntervals(params TimeSpan[] intervals)
    {
        RetryLimit = intervals.Length;
        _intervals = [.. intervals];
        return this;
    }
    /// <summary>
    /// Adds random jitter to retry intervals.
    /// </summary>
    /// <param name="factor">The jitter factor between 0.0 and 1.0.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="factor"/> is outside the valid range.</exception>
    /// <returns>The same configurator, for chaining.</returns>
    public RetryPolicyConfigurator UseJitter(double factor)
    {
        if (factor is < 0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), "Jitter must be between 0.0 and 1.0");
        }

        _jitterFactor = factor;
        return this;
    }
    /// <summary>
    /// Configures exponential back-off retry intervals.
    /// </summary>
    /// <param name="retryCount">The number of retries.</param>
    /// <param name="initialInterval">The delay for the first retry.</param>
    /// <param name="maxInterval">The maximum delay cap.</param>
    /// <param name="multiplier">The multiplier applied to each successive interval. Default is 2.0.</param>
    /// <returns>The same configurator, for chaining.</returns>
    public RetryPolicyConfigurator Exponential(
        int retryCount,
        TimeSpan initialInterval,
        TimeSpan maxInterval,
        double multiplier = 2.0)
    {
        RetryLimit = retryCount;
        var intervals = new List<TimeSpan>();
        var currentInterval = initialInterval;

        for (int i = 0; i < retryCount; i++)
        {
            intervals.Add(currentInterval);

            double nextTicks = currentInterval.Ticks * multiplier;
            currentInterval = TimeSpan.FromTicks((long)nextTicks);

            if (currentInterval > maxInterval)
            {
                currentInterval = maxInterval;
            }
        }

        _intervals = [.. intervals];
        return this;
    }

    /// <summary>
    /// Adds an exception type to the ignore list; matching exceptions will not trigger retries.
    /// </summary>
    /// <typeparam name="TException">The exception type to ignore.</typeparam>
    /// <returns>The same configurator, for chaining.</returns>
    public RetryPolicyConfigurator Ignore<TException>() where TException : Exception
    {
        _ignoredExceptions.Add(typeof(TException));
        return this;
    }

    /// <summary>
    /// Configures retries to run in-memory: the consumer is re-invoked in-process while the delivery is held,
    /// instead of being re-delivered later through the retry queue. This preserves message order, so it is the
    /// behavior used automatically for partitioned queues.
    /// </summary>
    /// <returns>The same configurator, for chaining.</returns>
    public RetryPolicyConfigurator InMemory()
    {
        _inMemory = true;
        return this;
    }

    /// <summary>
    /// Builds the <see cref="RetryPolicyDefinition"/> from the current configuration.
    /// </summary>
    /// <returns>The constructed retry policy definition.</returns>
    public RetryPolicyDefinition Build()
    {
        var definition = new RetryPolicyDefinition
        {
            MaxRetryCount = RetryLimit,
            JitterFactor = _jitterFactor,
            InMemory = _inMemory
        };

        foreach (var interval in _intervals)
        {
            definition.Intervals.Add(interval);
        }

        foreach (var type in _ignoredExceptions)
        {
            definition.AddIgnoredType(type);
            definition.IgnoreExceptions.Add(type.AssemblyQualifiedName!);
        }

        return definition;
    }
}
