using System.Security.Cryptography;
using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Queues;

/// <summary>
/// Configures a queue's consumers, retry policies, and dead letter settings.
/// </summary>
public interface IQueueConfigurator
{
    /// <summary>
    /// Registers a one-way consumer on this queue with optional per-consumer configuration.
    /// </summary>
    IQueueConfigurator AddConsumer<TConsumer>(Action<IConsumerConfigurator<TConsumer>>? configure = null) where TConsumer : class, IConsumer;
    /// <summary>
    /// Registers a request/reply consumer on this queue with optional per-consumer configuration.
    /// </summary>
    IQueueConfigurator AddRequestConsumer<TConsumer>(Action<IRequestConfigurator<TConsumer>>? configure = null) where TConsumer : class, IRequestConsumer;
    /// <summary>
    /// Applies additional configuration to the underlying <see cref="QueueDefinition"/>.
    /// </summary>
    IQueueConfigurator ConfigureQueue(Action<QueueDefinition> configureAction);

    /// <summary>
    /// Configures the default retry policy for all consumers on this queue.
    /// </summary>
    IQueueConfigurator UseRetry(Action<RetryPolicyConfigurator> configure);
    /// <summary>
    /// Enables a dead letter queue for this queue.
    /// </summary>
    /// <param name="queueName">Optional dead letter queue name.</param>
    /// <param name="exchangeName">Optional dead letter exchange name.</param>
    IQueueConfigurator UseDeadLetterQueue(string? queueName = null, string? exchangeName = null);
}

/// <summary>
/// Defines a retry policy including intervals, jitter, and exception filtering.
/// </summary>
public sealed record RetryPolicyDefinition
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetryCount { get; set; }
    /// <summary>
    /// Gets or sets the jitter factor (0.0–1.0) applied to retry intervals.
    /// </summary>
    public double JitterFactor { get; set; }
    /// <summary>
    /// Gets the delay intervals between successive retry attempts.
    /// </summary>
    public ICollection<TimeSpan> Intervals { get; } = [];
    /// <summary>
    /// Gets the fully-qualified type names of exceptions that should be excluded from retry attempts.
    /// </summary>
    public ICollection<string> IgnoreExceptions { get; } = [];

    private readonly HashSet<Type> _ignoredTypes = [];
    internal void AddIgnoredType(Type type) => _ignoredTypes.Add(type);
    /// <summary>
    /// Gets the resolved CLR exception types that should be excluded from retry attempts.
    /// Lazily resolves from <see cref="IgnoreExceptions"/> on first access.
    /// </summary>
    public HashSet<Type> GetIgnoredExceptionTypes()
    {
        if (IgnoreExceptions.Count > 0 && _ignoredTypes.Count == 0)
        {
            foreach (var name in IgnoreExceptions)
            {
                var t = Type.GetType(name, false);
                if (t != null)
                {
                    _ignoredTypes.Add(t);
                }
            }
        }

        return _ignoredTypes;
    }
    /// <summary>
    /// Calculates the delay for the specified retry attempt, applying jitter when configured.
    /// </summary>
    /// <param name="attempt">The zero-based attempt index.</param>
    /// <returns>The delay before the next retry.</returns>
    public TimeSpan GetDelay(int attempt)
    {
        var intervals = Intervals.ToList();
        if (intervals.Count == 0)
        {
            return TimeSpan.Zero;
        }
        var interval = (attempt >= intervals.Count) ? intervals[^1] : intervals[attempt];


        if (JitterFactor <= 0.0)
        {
            return interval;
        }


        var randomValue = RandomNumberGenerator.GetDouble();
        var jitterMultiplier = randomValue * 2 * JitterFactor - JitterFactor;

        var jitterDelta = interval.TotalMilliseconds * jitterMultiplier;

        var finalDelay = interval.TotalMilliseconds + jitterDelta;
        return TimeSpan.FromMilliseconds(Math.Max(0, finalDelay));
    }
}

internal static class RandomNumberGeneratorExtensions
{
    extension(RandomNumberGenerator)
    {
        /// <summary>
        /// Generates a cryptographically random <see langword="double"/> in the range [0, 1).
        /// </summary>
        public static double GetDouble()
        {
            var bytes = RandomNumberGenerator.GetBytes(8);
            var ul = BitConverter.ToUInt64(bytes, 0) / (1 << 11);
            return ul / (double)(1UL << 53);
        }
    }
}

/// <summary>
/// Describes the dead letter queue and exchange configuration.
/// </summary>
public sealed record DeadLetterDefinition
{
    /// <summary>
    /// Gets or sets the dead letter queue name, or <see langword="null"/> to use the default.
    /// </summary>
    public string? QueueName { get; set; }
    /// <summary>
    /// Gets or sets the dead letter exchange name, or <see langword="null"/> to use the default.
    /// </summary>
    public string? ExchangeName { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether dead letter routing is enabled.
    /// </summary>
    public bool Enabled { get; set; }
}

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
    public void Immediate(int retryCount)
    {
        RetryLimit = retryCount;
        _intervals = [.. Enumerable.Repeat(TimeSpan.Zero, retryCount)];
    }

    /// <summary>
    /// Configures explicit retry intervals.
    /// </summary>
    /// <param name="intervals">The delay between each retry attempt.</param>
    public void SetIntervals(params TimeSpan[] intervals)
    {
        RetryLimit = intervals.Length;
        _intervals = [.. intervals];
    }
    /// <summary>
    /// Adds random jitter to retry intervals.
    /// </summary>
    /// <param name="factor">The jitter factor between 0.0 and 1.0.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="factor"/> is outside the valid range.</exception>
    public void UseJitter(double factor)
    {
        if (factor is < 0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), "Jitter must be between 0.0 and 1.0");
        }

        _jitterFactor = factor;
    }
    /// <summary>
    /// Configures exponential back-off retry intervals.
    /// </summary>
    /// <param name="retryCount">The number of retries.</param>
    /// <param name="initialInterval">The delay for the first retry.</param>
    /// <param name="maxInterval">The maximum delay cap.</param>
    /// <param name="multiplier">The multiplier applied to each successive interval. Default is 2.0.</param>
    public void Exponential(
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

            // Calculate next interval
            double nextTicks = currentInterval.Ticks * multiplier;
            currentInterval = TimeSpan.FromTicks((long)nextTicks);

            // Cap it at the max interval
            if (currentInterval > maxInterval)
            {
                currentInterval = maxInterval;
            }
        }

        _intervals = [.. intervals];
    }

    /// <summary>
    /// Adds an exception type to the ignore list; matching exceptions will not trigger retries.
    /// </summary>
    /// <typeparam name="TException">The exception type to ignore.</typeparam>
    public void Ignore<TException>() where TException : Exception => _ignoredExceptions.Add(typeof(TException));

    /// <summary>
    /// Builds the <see cref="RetryPolicyDefinition"/> from the current configuration.
    /// </summary>
    /// <returns>The constructed retry policy definition.</returns>
    public RetryPolicyDefinition Build()
    {
        var definition = new RetryPolicyDefinition
        {
            MaxRetryCount = RetryLimit,
            JitterFactor = _jitterFactor
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
