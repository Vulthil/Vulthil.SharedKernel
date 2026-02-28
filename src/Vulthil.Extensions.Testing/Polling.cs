using Vulthil.Results;

namespace Vulthil.Extensions.Testing;

/// <summary>
/// Provides polling utilities for waiting on asynchronous conditions during tests.
/// </summary>
public static class Polling
{
    private static readonly Error Timeout =
        Error.Failure("Polling.Timeout", "The poll timed out.");

    /// <summary>
    /// Polls the provided function at regular intervals until it returns a successful result or the timeout expires.
    /// </summary>
    /// <typeparam name="T">The type of the expected value.</typeparam>
    /// <param name="timeout">The maximum duration to poll.</param>
    /// <param name="func">The function to invoke each tick.</param>
    /// <param name="timerTick">The interval between polls. Default is 1 second.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The first successful result, or a timeout failure.</returns>
    public static async Task<Result<T>> WaitAsync<T>(
        TimeSpan timeout,
        Func<Task<Result<T>>> func,
        TimeSpan? timerTick = null,
        CancellationToken cancellationToken = default)
    {
        timerTick ??= TimeSpan.FromSeconds(1);
        using var timer = new PeriodicTimer(timerTick.Value);

        DateTime endTimeUtc = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < endTimeUtc &&
               await timer.WaitForNextTickAsync(cancellationToken))
        {
            Result<T> result = await func();

            if (result.IsSuccess)
            {
                return result;
            }
        }

        return Result.Failure<T>(Timeout);
    }

    /// <summary>
    /// Polls the provided function at regular intervals until it returns a successful result or the timeout expires.
    /// </summary>
    /// <param name="timeout">The maximum duration to poll.</param>
    /// <param name="func">The function to invoke each tick.</param>
    /// <param name="timerTick">The interval between polls. Default is 1 second.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A successful result, or a timeout failure.</returns>
    public static async Task<Result> WaitAsync(
        TimeSpan timeout,
        Func<Task<Result>> func,
        TimeSpan? timerTick = null,
        CancellationToken cancellationToken = default)
    {
        timerTick ??= TimeSpan.FromSeconds(1);
        using var timer = new PeriodicTimer(timerTick.Value);

        DateTime endTimeUtc = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < endTimeUtc &&
               await timer.WaitForNextTickAsync(cancellationToken))
        {
            Result result = await func();

            if (result.IsSuccess)
            {
                return result;
            }
        }

        return Result.Failure(Timeout);
    }
}
