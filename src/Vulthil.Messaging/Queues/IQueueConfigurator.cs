using System.Reflection;
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
    /// If the consumer's <c>TMessage</c> is concrete, the queue is auto-subscribed to it at build time;
    /// for polymorphic consumers (e.g. <c>IConsumer&lt;IOrderEvent&gt;</c>) the caller must explicitly
    /// <see cref="Subscribe{TMessage}"/> the concrete implementers.
    /// </summary>
    IQueueConfigurator AddConsumer<TConsumer>(Action<IConsumerConfigurator<TConsumer>>? configure = null) where TConsumer : class, IConsumer;
    /// <summary>
    /// Registers a request/reply consumer on this queue with optional per-consumer configuration.
    /// </summary>
    IQueueConfigurator AddRequestConsumer<TConsumer>(Action<IRequestConfigurator<TConsumer>>? configure = null) where TConsumer : class, IRequestConsumer;

    /// <summary>
    /// Subscribes this queue to receive deliveries of the concrete message type <typeparamref name="TMessage"/>.
    /// At topology setup time, the queue is bound to <typeparamref name="TMessage"/>'s exchange with the supplied
    /// <paramref name="routingKey"/> pattern (the broker uses this to filter; the worker does not re-filter).
    /// Abstract types and interfaces are rejected — those have no exchange; use <see cref="SubscribeAll{TInterface}"/>
    /// or call <see cref="Subscribe{TMessage}"/> for each concrete implementer instead.
    /// </summary>
    /// <typeparam name="TMessage">A concrete (non-abstract, non-interface) message type.</typeparam>
    /// <param name="routingKey">
    /// The binding pattern. When <see langword="null"/>, the broker receives an empty string —
    /// fanout/headers exchanges ignore it; direct exchanges only deliver messages with an empty
    /// published routing key; topic exchanges match no patterns. For non-empty needs, supply a specific
    /// pattern (e.g. <c>"order.created"</c> for direct, <c>"order.*"</c> for topic).
    /// </param>
    IQueueConfigurator Subscribe<TMessage>(string? routingKey = null) where TMessage : class;

    /// <summary>
    /// Discovers every concrete (non-abstract, non-interface) type in <paramref name="assembly"/> that is
    /// assignable to <typeparamref name="TInterface"/>, and calls <see cref="Subscribe{TMessage}"/> for each.
    /// Pair with a polymorphic <c>AddConsumer&lt;TConsumer&gt;()</c> (where <c>TConsumer : IConsumer&lt;TInterface&gt;</c>)
    /// to dispatch all implementers through one consumer.
    /// </summary>
    /// <typeparam name="TInterface">The polymorphic dispatch type — typically an interface or abstract base class.</typeparam>
    /// <param name="assembly">The assembly to scan for concrete implementers.</param>
    /// <param name="routingKey">The binding pattern applied to every discovered implementer's exchange. <see langword="null"/> = broker default.</param>
    IQueueConfigurator SubscribeAll<TInterface>(Assembly assembly, string? routingKey = null) where TInterface : class;

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

    /// <summary>
    /// Declares the queue with RabbitMQ's single active consumer feature: only one consumer is active at a
    /// time while additional consumers stand by and take over on failure. This preserves per-queue order
    /// across load-balanced consumer instances — extending the in-process partitioner's ordering guarantee
    /// to multiple instances — at the cost of throughput scale-out for the queue. Partitioned queues enable
    /// this automatically.
    /// </summary>
    IQueueConfigurator UseSingleActiveConsumer();
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
    /// Gets or sets a value indicating whether retries run in-memory — the consumer is re-invoked in-process
    /// while the delivery is held — rather than via delayed re-delivery through the retry queue. In-memory
    /// retries preserve message order (a later message cannot overtake the one being retried), so they are
    /// used automatically for partitioned queues. Defaults to <see langword="false"/> (delayed re-delivery).
    /// </summary>
    public bool InMemory { get; set; }
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


        var randomValue = RandomNumberGeneratorExtensions.GetDouble();
        var jitterMultiplier = randomValue * 2 * JitterFactor - JitterFactor;

        var jitterDelta = interval.TotalMilliseconds * jitterMultiplier;

        var finalDelay = interval.TotalMilliseconds + jitterDelta;
        return TimeSpan.FromMilliseconds(Math.Max(0, finalDelay));
    }
}

internal static class RandomNumberGeneratorExtensions
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
    /// Configures retries to run in-memory: the consumer is re-invoked in-process while the delivery is held,
    /// instead of being re-delivered later through the retry queue. This preserves message order, so it is the
    /// behavior used automatically for partitioned queues.
    /// </summary>
    public void InMemory() => _inMemory = true;

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
