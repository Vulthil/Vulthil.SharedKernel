using System.Security.Cryptography;
using Vulthil.Messaging.Abstractions.Consumers;

namespace Vulthil.Messaging.Queues;

public interface IQueueConfigurator
{
    IQueueConfigurator AddConsumer<TConsumer>(Action<IConsumerConfigurator<TConsumer>>? configure = null) where TConsumer : class, IConsumer;
    IQueueConfigurator AddRequestConsumer<TConsumer>(Action<IRequestConfigurator<TConsumer>>? configure = null) where TConsumer : class, IRequestConsumer;
    IQueueConfigurator ConfigureQueue(Action<QueueDefinition> configureAction);

    IQueueConfigurator UseRetry(Action<RetryPolicyConfigurator> configure);
    IQueueConfigurator UseDeadLetterQueue(string? queueName = null, string? exchangeName = null);
}

public sealed record RetryPolicyDefinition
{
    public int MaxRetryCount { get; set; }
    public double JitterFactor { get; set; }
    public ICollection<TimeSpan> Intervals { get; } = [];
    public ICollection<string> IgnoreExceptions { get; } = [];

    private readonly HashSet<Type> _ignoredTypes = [];
    internal void AddIgnoredType(Type type) => _ignoredTypes.Add(type);
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
        public static double GetDouble()
        {
            var bytes = RandomNumberGenerator.GetBytes(8);
            var ul = BitConverter.ToUInt64(bytes, 0) / (1 << 11);
            return ul / (double)(1UL << 53);
        }
    }
}

public sealed record DeadLetterDefinition
{
    public string? QueueName { get; set; }
    public string? ExchangeName { get; set; }
    public bool Enabled { get; set; }
}

public sealed class RetryPolicyConfigurator
{
    private readonly HashSet<Type> _ignoredExceptions = [];
    private List<TimeSpan> _intervals = [];
    public int RetryLimit { get; private set; }
    private double _jitterFactor;
    public IReadOnlyList<TimeSpan> Intervals => _intervals;
    public IReadOnlySet<Type> IgnoredExceptions => _ignoredExceptions;

    public void Immediate(int retryCount)
    {
        RetryLimit = retryCount;
        _intervals = [.. Enumerable.Repeat(TimeSpan.Zero, retryCount)];
    }

    public void SetIntervals(params TimeSpan[] intervals)
    {
        RetryLimit = intervals.Length;
        _intervals = [.. intervals];
    }
    public void UseJitter(double factor)
    {
        if (factor is < 0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), "Jitter must be between 0.0 and 1.0");
        }

        _jitterFactor = factor;
    }
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

    public void Ignore<TException>() where TException : Exception => _ignoredExceptions.Add(typeof(TException));

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
