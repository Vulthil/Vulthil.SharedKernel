namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Decides how long the relay waits before its next cycle.
/// </summary>
internal static class OutboxRelayBackoff
{
    /// <summary>
    /// Returns the wait after a completed cycle: none after a full batch that relayed completely, because more
    /// messages are likely waiting; the base delay after any other cycle that relayed something; and twice the previous
    /// wait — at least the base delay and at most the maximum delay — after a cycle that relayed nothing, because there
    /// was no pending message or every claimed message failed.
    /// </summary>
    /// <param name="cycle">The outcome of the cycle that just completed.</param>
    /// <param name="previousDelay">The wait before that cycle.</param>
    /// <param name="options">The relay options that set the batch size and the delays.</param>
    /// <returns>The wait before the next cycle.</returns>
    public static TimeSpan After(OutboxRelayCycleResult cycle, TimeSpan previousDelay, OutboxProcessingOptions options)
    {
        if (cycle.Relayed >= options.BatchSize)
        {
            return TimeSpan.Zero;
        }

        return cycle.Relayed > 0 ? BaseDelay(options) : Doubled(previousDelay, options);
    }

    /// <summary>
    /// Returns the wait after a cycle that faulted before it completed: twice the previous wait — at least the base delay
    /// and at most the maximum delay — so a store or broker that stays unavailable is tried less and less often. The
    /// next completed cycle that relays something brings the wait back to the base delay.
    /// </summary>
    /// <param name="previousDelay">The wait before the cycle that faulted.</param>
    /// <param name="options">The relay options that set the delays.</param>
    /// <returns>The wait before the next cycle.</returns>
    public static TimeSpan AfterFault(TimeSpan previousDelay, OutboxProcessingOptions options) => Doubled(previousDelay, options);

    private static TimeSpan Doubled(TimeSpan previousDelay, OutboxProcessingOptions options)
    {
        var baseDelay = BaseDelay(options);
        var doubled = previousDelay * 2;
        var atLeastBase = doubled < baseDelay ? baseDelay : doubled;
        var maxDelay = TimeSpan.FromSeconds(options.MaxDelaySeconds);
        return atLeastBase > maxDelay ? maxDelay : atLeastBase;
    }

    private static TimeSpan BaseDelay(OutboxProcessingOptions options) => TimeSpan.FromSeconds(options.OutboxProcessingDelaySeconds);
}
