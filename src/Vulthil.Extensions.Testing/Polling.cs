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
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    public static Task<PollingResult<T>> WaitAsync<T>(
        TimeSpan timeout,
        Func<Task<Result<T>>> func)
        => WaitAsync<T>(timeout, IgnoreToken(func), DefaultTimerTick, TimeProvider.System, CancellationToken.None);

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
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    public static Task<PollingResult<T>> WaitAsync<T>(
        TimeSpan timeout,
        Func<Task<Result<T>>> func,
        CancellationToken cancellationToken)
        => WaitAsync<T>(timeout, IgnoreToken(func), DefaultTimerTick, TimeProvider.System, cancellationToken);

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
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    public static Task<PollingResult<T>> WaitAsync<T>(
        TimeSpan timeout,
        Func<Task<Result<T>>> func,
        TimeSpan timerTick,
        CancellationToken cancellationToken)
        => WaitAsync<T>(timeout, IgnoreToken(func), timerTick, TimeProvider.System, cancellationToken);

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
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    public static Task<PollingResult<T>> WaitAsync<T>(
        TimeSpan timeout,
        Func<CancellationToken, Task<Result<T>>> func,
        CancellationToken cancellationToken)
        => WaitAsync<T>(timeout, func, DefaultTimerTick, TimeProvider.System, cancellationToken);

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
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    public static Task<PollingResult<T>> WaitAsync<T>(
        TimeSpan timeout,
        Func<CancellationToken, Task<Result<T>>> func,
        TimeSpan timerTick,
        CancellationToken cancellationToken)
        => WaitAsync<T>(timeout, func, timerTick, TimeProvider.System, cancellationToken);

    internal static Task<PollingResult<T>> WaitAsync<T>(
        TimeSpan timeout,
        Func<CancellationToken, Task<Result<T>>>? func,
        TimeSpan timerTick,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
        => PollAsync(
            timeout,
            func,
            timerTick,
            timeProvider,
            static result => PollingResult<T>.CreateSuccess(result.Value),
            PollingResult<T>.CreateTimeout,
            cancellationToken);

    /// <summary>
    /// Polls the provided function at one-second intervals until it returns a successful result or the timeout expires.
    /// </summary>
    /// <param name="timeout">The maximum duration to poll.</param>
    /// <param name="func">The function to invoke each tick.</param>
    /// <returns>
    /// A <see cref="PollingResult"/> containing a success indication,
    /// or a <see cref="PollingError"/> with all errors collected during polling.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    public static Task<PollingResult> WaitAsync(
        TimeSpan timeout,
        Func<Task<Result>> func)
        => WaitAsync(timeout, IgnoreToken(func), DefaultTimerTick, TimeProvider.System, CancellationToken.None);

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
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    public static Task<PollingResult> WaitAsync(
        TimeSpan timeout,
        Func<Task<Result>> func,
        CancellationToken cancellationToken)
        => WaitAsync(timeout, IgnoreToken(func), DefaultTimerTick, TimeProvider.System, cancellationToken);

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
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    public static Task<PollingResult> WaitAsync(
        TimeSpan timeout,
        Func<Task<Result>> func,
        TimeSpan timerTick,
        CancellationToken cancellationToken)
        => WaitAsync(timeout, IgnoreToken(func), timerTick, TimeProvider.System, cancellationToken);

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
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    public static Task<PollingResult> WaitAsync(
        TimeSpan timeout,
        Func<CancellationToken, Task<Result>> func,
        CancellationToken cancellationToken)
        => WaitAsync(timeout, func, DefaultTimerTick, TimeProvider.System, cancellationToken);

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
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    public static Task<PollingResult> WaitAsync(
        TimeSpan timeout,
        Func<CancellationToken, Task<Result>> func,
        TimeSpan timerTick,
        CancellationToken cancellationToken)
        => WaitAsync(timeout, func, timerTick, TimeProvider.System, cancellationToken);

    internal static Task<PollingResult> WaitAsync(
        TimeSpan timeout,
        Func<CancellationToken, Task<Result>>? func,
        TimeSpan timerTick,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
        => PollAsync(
            timeout,
            func,
            timerTick,
            timeProvider,
            static _ => PollingResult.CreateSuccess(),
            PollingResult.CreateTimeout,
            cancellationToken);

    private static async Task<TPollingResult> PollAsync<TResult, TPollingResult>(
        TimeSpan timeout,
        Func<CancellationToken, Task<TResult>>? func,
        TimeSpan timerTick,
        TimeProvider timeProvider,
        Func<TResult, TPollingResult> onSuccess,
        Func<PollingError, TPollingResult> onTimeout,
        CancellationToken cancellationToken)
        where TResult : Result
    {
        ArgumentNullException.ThrowIfNull(func);

        using var timeoutCts = new CancellationTokenSource(timeout, timeProvider);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        using var timer = new PeriodicTimer(timerTick, timeProvider);

        List<Error> errors = [];

        try
        {
            do
            {
                TResult result = await func(linkedCts.Token).ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    return onSuccess(result);
                }

                errors.Add(result.Error);
            }
            while (await timer.WaitForNextTickAsync(linkedCts.Token).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Polling timeout reached; surface the aggregated errors below.
        }

        return onTimeout(PollingError.FromErrors(errors));
    }

    // A null func passes through, so the core rejects it with ArgumentNullException when the poll is awaited,
    // as it does for every other overload.
    private static Func<CancellationToken, Task<TResult>>? IgnoreToken<TResult>(Func<Task<TResult>>? func)
        => func is null ? null : _ => func();
}
