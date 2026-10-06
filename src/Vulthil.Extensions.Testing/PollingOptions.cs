namespace Vulthil.Extensions.Testing;

/// <summary>
/// Configures a <see cref="Polling"/> wait: how long to poll, how often to poll, and the clock the poll runs on.
/// </summary>
/// <remarks>
/// Set <see cref="TimeProvider"/> to a fake clock, such as <c>FakeTimeProvider</c>, to drive a poll from a test:
/// the poll then ticks and times out only when the test advances that clock.
/// </remarks>
/// <param name="Timeout">The maximum duration to poll.</param>
public sealed record PollingOptions(TimeSpan Timeout)
{
    internal static readonly TimeSpan DefaultTimerTick = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets the interval between polls. Default is one second.
    /// </summary>
    public TimeSpan TimerTick { get; init; } = DefaultTimerTick;

    /// <summary>
    /// Gets the clock that the timer and the timeout run on. Default is <see cref="System.TimeProvider.System"/>.
    /// </summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}
