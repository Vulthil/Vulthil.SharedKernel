using Vulthil.Results;

namespace Vulthil.Extensions.Testing;

/// <summary>
/// Provides polling utilities for waiting on asynchronous conditions during tests.
/// </summary>
public static class Polling
{
    /// <summary>
    /// Represents an error indicating that a polling operation has timed out.
    /// </summary>
    /// <remarks>
    /// Use this error to signal that a polling process did not complete within the allotted time.
    /// This error can be used to distinguish timeout conditions from other types of failures when handling polling
    /// results.
    /// </remarks>
    public static readonly Error Timeout =
        Error.Failure("Polling.Timeout", "The poll timed out.");

    private static readonly TimeSpan DefaultTimerTick = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Polls the provided function at one-second intervals until it returns a successful result or the timeout expires.
    /// </summary>
    /// <typeparam name="T">The type of the expected value.</typeparam>
    /// <param name="timeout">The maximum duration to poll.</param>
    /// <param name="func">The function to invoke each tick.</param>
    /// <returns>
    /// A <see cref="PollingResult{T}"/> containing the first successful result,
    /// or a <see cref="PollingError"/> with all errors collected during polling.
    /// </returns>
    public static Task<PollingResult<T>> WaitAsync<T>(
        TimeSpan timeout,
        Func<Task<Result<T>>> func)
        => WaitAsync<T>(timeout, _ => func(), DefaultTimerTick, CancellationToken.None);

    /// <summary>
    /// Polls the provided function at one-second intervals until it returns a successful result or the timeout expires.
    /// </summary>
    /// <typeparam name="T">The type of the expected value.</typeparam>
    /// <param name="timeout">The maximum duration to poll.</param>
    /// <param name="func">The function to invoke each tick.</param>
    /// <param name="cancellationToken">A token to observe for cancellation. Linked internally with the polling timeout.</param>
    /// <returns>
    /// A <see cref="PollingResult{T}"/> containing the first successful result,
    /// or a <see cref="PollingError"/> with all errors collected during polling.
    /// </returns>
    public static Task<PollingResult<T>> WaitAsync<T>(
        TimeSpan timeout,
        Func<Task<Result<T>>> func,
        CancellationToken cancellationToken)
        => WaitAsync<T>(timeout, _ => func(), DefaultTimerTick, cancellationToken);

    /// <summary>
    /// Polls the provided function at regular intervals until it returns a successful result or the timeout expires.
    /// </summary>
    /// <typeparam name="T">The type of the expected value.</typeparam>
    /// <param name="timeout">The maximum duration to poll.</param>
    /// <param name="func">The function to invoke each tick.</param>
    /// <param name="timerTick">The interval between polls.</param>
    /// <param name="cancellationToken">A token to observe for cancellation. Linked internally with the polling timeout.</param>
    /// <returns>
    /// A <see cref="PollingResult{T}"/> containing the first successful result,
    /// or a <see cref="PollingError"/> with all errors collected during polling.
    /// </returns>
    public static Task<PollingResult<T>> WaitAsync<T>(
        TimeSpan timeout,
        Func<Task<Result<T>>> func,
        TimeSpan timerTick,
        CancellationToken cancellationToken)
        => WaitAsync<T>(timeout, _ => func(), timerTick, cancellationToken);

    /// <summary>
    /// Polls the provided function at one-second intervals until it returns a successful result or the timeout expires.
    /// The combined polling/timeout cancellation token is forwarded to the function so it can short-circuit work in progress.
    /// </summary>
    /// <typeparam name="T">The type of the expected value.</typeparam>
    /// <param name="timeout">The maximum duration to poll.</param>
    /// <param name="func">The function to invoke each tick. Receives the shared cancellation token.</param>
    /// <param name="cancellationToken">A token to observe for cancellation. Linked internally with the polling timeout.</param>
    /// <returns>
    /// A <see cref="PollingResult{T}"/> containing the first successful result,
    /// or a <see cref="PollingError"/> with all errors collected during polling.
    /// </returns>
    public static Task<PollingResult<T>> WaitAsync<T>(
        TimeSpan timeout,
        Func<CancellationToken, Task<Result<T>>> func,
        CancellationToken cancellationToken)
        => WaitAsync<T>(timeout, func, DefaultTimerTick, cancellationToken);

    /// <summary>
    /// Polls the provided function at regular intervals until it returns a successful result or the timeout expires.
    /// The combined polling/timeout cancellation token is forwarded to the function so it can short-circuit work in progress.
    /// </summary>
    /// <typeparam name="T">The type of the expected value.</typeparam>
    /// <param name="timeout">The maximum duration to poll.</param>
    /// <param name="func">The function to invoke each tick. Receives the shared cancellation token.</param>
    /// <param name="timerTick">The interval between polls.</param>
    /// <param name="cancellationToken">A token to observe for cancellation. Linked internally with the polling timeout.</param>
    /// <returns>
    /// A <see cref="PollingResult{T}"/> containing the first successful result,
    /// or a <see cref="PollingError"/> with all errors collected during polling.
    /// </returns>
    public static async Task<PollingResult<T>> WaitAsync<T>(
        TimeSpan timeout,
        Func<CancellationToken, Task<Result<T>>> func,
        TimeSpan timerTick,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(func);

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        using var timer = new PeriodicTimer(timerTick);

        List<Error> errors = [];

        try
        {
            do
            {
                Result<T> result = await func(linkedCts.Token).ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    return PollingResult<T>.CreateSuccess(result.Value);
                }

                errors.Add(result.Error);
            }
            while (await timer.WaitForNextTickAsync(linkedCts.Token).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Polling timeout reached; surface the aggregated errors below.
        }

        return PollingResult<T>.CreateTimeout(PollingError.FromErrors(errors));
    }

    /// <summary>
    /// Polls the provided function at one-second intervals until it returns a successful result or the timeout expires.
    /// </summary>
    /// <param name="timeout">The maximum duration to poll.</param>
    /// <param name="func">The function to invoke each tick.</param>
    /// <returns>
    /// A <see cref="PollingResult"/> containing a success indication,
    /// or a <see cref="PollingError"/> with all errors collected during polling.
    /// </returns>
    public static Task<PollingResult> WaitAsync(
        TimeSpan timeout,
        Func<Task<Result>> func)
        => WaitAsync(timeout, _ => func(), DefaultTimerTick, CancellationToken.None);

    /// <summary>
    /// Polls the provided function at one-second intervals until it returns a successful result or the timeout expires.
    /// </summary>
    /// <param name="timeout">The maximum duration to poll.</param>
    /// <param name="func">The function to invoke each tick.</param>
    /// <param name="cancellationToken">A token to observe for cancellation. Linked internally with the polling timeout.</param>
    /// <returns>
    /// A <see cref="PollingResult"/> containing a success indication,
    /// or a <see cref="PollingError"/> with all errors collected during polling.
    /// </returns>
    public static Task<PollingResult> WaitAsync(
        TimeSpan timeout,
        Func<Task<Result>> func,
        CancellationToken cancellationToken)
        => WaitAsync(timeout, _ => func(), DefaultTimerTick, cancellationToken);

    /// <summary>
    /// Polls the provided function at regular intervals until it returns a successful result or the timeout expires.
    /// </summary>
    /// <param name="timeout">The maximum duration to poll.</param>
    /// <param name="func">The function to invoke each tick.</param>
    /// <param name="timerTick">The interval between polls.</param>
    /// <param name="cancellationToken">A token to observe for cancellation. Linked internally with the polling timeout.</param>
    /// <returns>
    /// A <see cref="PollingResult"/> containing a success indication,
    /// or a <see cref="PollingError"/> with all errors collected during polling.
    /// </returns>
    public static Task<PollingResult> WaitAsync(
        TimeSpan timeout,
        Func<Task<Result>> func,
        TimeSpan timerTick,
        CancellationToken cancellationToken)
        => WaitAsync(timeout, _ => func(), timerTick, cancellationToken);

    /// <summary>
    /// Polls the provided function at one-second intervals until it returns a successful result or the timeout expires.
    /// The combined polling/timeout cancellation token is forwarded to the function so it can short-circuit work in progress.
    /// </summary>
    /// <param name="timeout">The maximum duration to poll.</param>
    /// <param name="func">The function to invoke each tick. Receives the shared cancellation token.</param>
    /// <param name="cancellationToken">A token to observe for cancellation. Linked internally with the polling timeout.</param>
    /// <returns>
    /// A <see cref="PollingResult"/> containing a success indication,
    /// or a <see cref="PollingError"/> with all errors collected during polling.
    /// </returns>
    public static Task<PollingResult> WaitAsync(
        TimeSpan timeout,
        Func<CancellationToken, Task<Result>> func,
        CancellationToken cancellationToken)
        => WaitAsync(timeout, func, DefaultTimerTick, cancellationToken);

    /// <summary>
    /// Polls the provided function at regular intervals until it returns a successful result or the timeout expires.
    /// The combined polling/timeout cancellation token is forwarded to the function so it can short-circuit work in progress.
    /// </summary>
    /// <param name="timeout">The maximum duration to poll.</param>
    /// <param name="func">The function to invoke each tick. Receives the shared cancellation token.</param>
    /// <param name="timerTick">The interval between polls.</param>
    /// <param name="cancellationToken">A token to observe for cancellation. Linked internally with the polling timeout.</param>
    /// <returns>
    /// A <see cref="PollingResult"/> containing a success indication,
    /// or a <see cref="PollingError"/> with all errors collected during polling.
    /// </returns>
    public static async Task<PollingResult> WaitAsync(
        TimeSpan timeout,
        Func<CancellationToken, Task<Result>> func,
        TimeSpan timerTick,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(func);

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        using var timer = new PeriodicTimer(timerTick);

        List<Error> errors = [];

        try
        {
            do
            {
                Result result = await func(linkedCts.Token).ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    return PollingResult.CreateSuccess();
                }

                errors.Add(result.Error);
            }
            while (await timer.WaitForNextTickAsync(linkedCts.Token).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Polling timeout reached; surface the aggregated errors below.
        }

        return PollingResult.CreateTimeout(PollingError.FromErrors(errors));
    }
}
