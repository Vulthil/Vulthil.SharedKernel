namespace Vulthil.Messaging.Queues;

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
    /// Names outside the core library must be assembly-qualified to resolve; a name that cannot be
    /// resolved is skipped (the RabbitMQ transport logs a startup warning for each such name).
    /// </summary>
    public ICollection<string> IgnoreExceptions { get; } = [];

    private readonly object _ignoredTypesGate = new();
    private readonly HashSet<Type> _ignoredTypes = [];
    private HashSet<Type>? _resolvedIgnoredTypes;

    internal void AddIgnoredType(Type type) => _ignoredTypes.Add(type);
    /// <summary>
    /// Gets the resolved CLR exception types that should be excluded from retry attempts. The set is built
    /// once, on first access, from the fluently ignored types and the resolvable names in
    /// <see cref="IgnoreExceptions"/>; the resolution is thread-safe and later mutations of either
    /// collection do not change the result.
    /// </summary>
    public HashSet<Type> GetIgnoredExceptionTypes()
    {
        lock (_ignoredTypesGate)
        {
            return _resolvedIgnoredTypes ??= ResolveIgnoredExceptionTypes();
        }
    }

    private HashSet<Type> ResolveIgnoredExceptionTypes()
    {
        var resolved = new HashSet<Type>(_ignoredTypes);
        foreach (var name in IgnoreExceptions)
        {
            if (Type.GetType(name, false) is { } type)
            {
                resolved.Add(type);
            }
        }

        return resolved;
    }

    /// <summary>
    /// Calculates the delay for the specified retry attempt, applying jitter when configured.
    /// Attempts beyond the last configured interval reuse that interval (capped back-off).
    /// </summary>
    /// <param name="attempt">The zero-based attempt index.</param>
    /// <returns>The delay before the next retry.</returns>
    public TimeSpan GetDelay(int attempt)
    {
        if (Intervals.Count == 0)
        {
            return TimeSpan.Zero;
        }

        var interval = GetIntervalForAttempt(attempt);
        if (JitterFactor <= 0.0)
        {
            return interval;
        }

        var jitterMultiplier = RandomNumberGeneratorExtensions.GetDouble() * 2 * JitterFactor - JitterFactor;
        var finalDelay = interval.TotalMilliseconds * (1 + jitterMultiplier);
        return TimeSpan.FromMilliseconds(Math.Max(0, finalDelay));
    }

    private TimeSpan GetIntervalForAttempt(int attempt)
    {
        var index = Math.Min(attempt, Intervals.Count - 1);
        return Intervals is IList<TimeSpan> list ? list[index] : Intervals.ElementAt(index);
    }
}
