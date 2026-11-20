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

    /// <summary>
    /// Represents a failed attempt of a condition poll: the condition returned <see langword="false"/>.
    /// </summary>
    public static readonly Error ConditionNotMet =
        Error.Failure("Polling.ConditionNotMet", "The condition was not met.");

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
        => PollAsync<T>(new PollingOptions(timeout), IgnoreToken(func), CancellationToken.None);

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
        => PollAsync<T>(new PollingOptions(timeout), IgnoreToken(func), cancellationToken);

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
        => PollAsync<T>(new PollingOptions(timeout) { TimerTick = timerTick }, IgnoreToken(func), cancellationToken);

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
        => PollAsync<T>(new PollingOptions(timeout), func, cancellationToken);

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
        => PollAsync<T>(new PollingOptions(timeout) { TimerTick = timerTick }, func, cancellationToken);

    /// <summary>
    /// Polls the provided function at regular intervals on <paramref name="timeProvider"/> until it returns a successful
    /// result or the timeout expires.
    /// The combined polling/timeout cancellation token is forwarded to the function so it can short-circuit work in progress.
    /// </summary>
    /// <remarks>
    /// The timer and the timeout both run on <paramref name="timeProvider"/>. With a fake clock, such as
    /// <c>FakeTimeProvider</c>, the poll ticks and times out only when the test advances that clock.
    /// </remarks>
    /// <typeparam name="T">The type of the expected value.</typeparam>
    /// <param name="timeout">The maximum duration to poll.</param>
    /// <param name="func">The function to invoke each tick. Receives the shared cancellation token.</param>
    /// <param name="timerTick">The interval between polls.</param>
    /// <param name="timeProvider">The clock that the timer and the timeout run on.</param>
    /// <param name="cancellationToken">A token to observe for cancellation. Linked internally with the polling timeout.</param>
    /// <returns>
    /// A <see cref="PollingResult{T}"/> containing the first successful result,
    /// or a <see cref="PollingError"/> with all errors collected during polling.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="func"/> or <paramref name="timeProvider"/> is <see langword="null"/>.
    /// </exception>
    public static Task<PollingResult<T>> WaitAsync<T>(
        TimeSpan timeout,
        Func<CancellationToken, Task<Result<T>>> func,
        TimeSpan timerTick,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
        => PollAsync<T>(new PollingOptions(timeout) { TimerTick = timerTick, TimeProvider = timeProvider }, func, cancellationToken);

    /// <summary>
    /// Polls the provided function with the settings of <paramref name="options"/> until it returns a successful result
    /// or the timeout expires.
    /// The combined polling/timeout cancellation token is forwarded to the function so it can short-circuit work in progress.
    /// </summary>
    /// <remarks>
    /// An exception that <see cref="PollingOptions.TreatAsFailedAttempt"/> accepts counts as a failed attempt and is
    /// recorded as a <see cref="PollingExceptionError"/>; any other exception ends the poll and reaches the caller.
    /// </remarks>
    /// <typeparam name="T">The type of the expected value.</typeparam>
    /// <param name="options">
    /// The timeout, the interval between polls, the clock to poll on, and the exceptions that count as failed attempts.
    /// </param>
    /// <param name="func">The function to invoke each tick. Receives the shared cancellation token.</param>
    /// <param name="cancellationToken">A token to observe for cancellation. Linked internally with the polling timeout.</param>
    /// <returns>
    /// A <see cref="PollingResult{T}"/> containing the first successful result,
    /// or a <see cref="PollingError"/> with all errors collected during polling.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/>, its <see cref="PollingOptions.TimeProvider"/>, or <paramref name="func"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static Task<PollingResult<T>> WaitAsync<T>(
        PollingOptions options,
        Func<CancellationToken, Task<Result<T>>> func,
        CancellationToken cancellationToken)
        => PollAsync<T>(options, func, cancellationToken);

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
        => PollAsync(new PollingOptions(timeout), IgnoreToken(func), CancellationToken.None);

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
        => PollAsync(new PollingOptions(timeout), IgnoreToken(func), cancellationToken);

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
        => PollAsync(new PollingOptions(timeout) { TimerTick = timerTick }, IgnoreToken(func), cancellationToken);

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
        => PollAsync(new PollingOptions(timeout), func, cancellationToken);

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
        => PollAsync(new PollingOptions(timeout) { TimerTick = timerTick }, func, cancellationToken);

    /// <summary>
    /// Polls the provided function at regular intervals on <paramref name="timeProvider"/> until it returns a successful
    /// result or the timeout expires.
    /// The combined polling/timeout cancellation token is forwarded to the function so it can short-circuit work in progress.
    /// </summary>
    /// <remarks>
    /// The timer and the timeout both run on <paramref name="timeProvider"/>. With a fake clock, such as
    /// <c>FakeTimeProvider</c>, the poll ticks and times out only when the test advances that clock.
    /// </remarks>
    /// <param name="timeout">The maximum duration to poll.</param>
    /// <param name="func">The function to invoke each tick. Receives the shared cancellation token.</param>
    /// <param name="timerTick">The interval between polls.</param>
    /// <param name="timeProvider">The clock that the timer and the timeout run on.</param>
    /// <param name="cancellationToken">A token to observe for cancellation. Linked internally with the polling timeout.</param>
    /// <returns>
    /// A <see cref="PollingResult"/> containing a success indication,
    /// or a <see cref="PollingError"/> with all errors collected during polling.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="func"/> or <paramref name="timeProvider"/> is <see langword="null"/>.
    /// </exception>
    public static Task<PollingResult> WaitAsync(
        TimeSpan timeout,
        Func<CancellationToken, Task<Result>> func,
        TimeSpan timerTick,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
        => PollAsync(new PollingOptions(timeout) { TimerTick = timerTick, TimeProvider = timeProvider }, func, cancellationToken);

    /// <summary>
    /// Polls the provided function with the settings of <paramref name="options"/> until it returns a successful result
    /// or the timeout expires.
    /// The combined polling/timeout cancellation token is forwarded to the function so it can short-circuit work in progress.
    /// </summary>
    /// <remarks>
    /// An exception that <see cref="PollingOptions.TreatAsFailedAttempt"/> accepts counts as a failed attempt and is
    /// recorded as a <see cref="PollingExceptionError"/>; any other exception ends the poll and reaches the caller.
    /// </remarks>
    /// <param name="options">
    /// The timeout, the interval between polls, the clock to poll on, and the exceptions that count as failed attempts.
    /// </param>
    /// <param name="func">The function to invoke each tick. Receives the shared cancellation token.</param>
    /// <param name="cancellationToken">A token to observe for cancellation. Linked internally with the polling timeout.</param>
    /// <returns>
    /// A <see cref="PollingResult"/> containing a success indication,
    /// or a <see cref="PollingError"/> with all errors collected during polling.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/>, its <see cref="PollingOptions.TimeProvider"/>, or <paramref name="func"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static Task<PollingResult> WaitAsync(
        PollingOptions options,
        Func<CancellationToken, Task<Result>> func,
        CancellationToken cancellationToken)
        => PollAsync(options, func, cancellationToken);

    /// <summary>
    /// Polls the provided condition at one-second intervals until it returns <see langword="true"/> or the timeout expires.
    /// The combined polling/timeout cancellation token is forwarded to the condition so it can short-circuit work in progress.
    /// </summary>
    /// <param name="timeout">The maximum duration to poll.</param>
    /// <param name="func">The condition to check each tick. Receives the shared cancellation token.</param>
    /// <param name="cancellationToken">A token to observe for cancellation. Linked internally with the polling timeout.</param>
    /// <returns>
    /// A <see cref="PollingResult"/> containing a success indication, or a <see cref="PollingError"/> that holds
    /// <see cref="ConditionNotMet"/> for each check that returned <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    public static Task<PollingResult> WaitAsync(
        TimeSpan timeout,
        Func<CancellationToken, Task<bool>> func,
        CancellationToken cancellationToken)
        => PollAsync(new PollingOptions(timeout), ToResult(func), cancellationToken);

    /// <summary>
    /// Polls the provided condition with the settings of <paramref name="options"/> until it returns
    /// <see langword="true"/> or the timeout expires.
    /// The combined polling/timeout cancellation token is forwarded to the condition so it can short-circuit work in progress.
    /// </summary>
    /// <remarks>
    /// An exception that <see cref="PollingOptions.TreatAsFailedAttempt"/> accepts counts as a failed attempt and is
    /// recorded as a <see cref="PollingExceptionError"/>; any other exception ends the poll and reaches the caller.
    /// </remarks>
    /// <param name="options">
    /// The timeout, the interval between polls, the clock to poll on, and the exceptions that count as failed attempts.
    /// </param>
    /// <param name="func">The condition to check each tick. Receives the shared cancellation token.</param>
    /// <param name="cancellationToken">A token to observe for cancellation. Linked internally with the polling timeout.</param>
    /// <returns>
    /// A <see cref="PollingResult"/> containing a success indication, or a <see cref="PollingError"/> that holds
    /// <see cref="ConditionNotMet"/> for each check that returned <see langword="false"/> and a
    /// <see cref="PollingExceptionError"/> for each exception that counted as a failed attempt.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/>, its <see cref="PollingOptions.TimeProvider"/>, or <paramref name="func"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static Task<PollingResult> WaitAsync(
        PollingOptions options,
        Func<CancellationToken, Task<bool>> func,
        CancellationToken cancellationToken)
        => PollAsync(options, ToResult(func), cancellationToken);

    private static Task<PollingResult<T>> PollAsync<T>(
        PollingOptions options,
        Func<CancellationToken, Task<Result<T>>>? func,
        CancellationToken cancellationToken)
        => PollAsync(
            options,
            func,
            static result => PollingResult<T>.CreateSuccess(result.Value),
            PollingResult<T>.CreateTimeout,
            cancellationToken);

    private static Task<PollingResult> PollAsync(
        PollingOptions options,
        Func<CancellationToken, Task<Result>>? func,
        CancellationToken cancellationToken)
        => PollAsync(
            options,
            func,
            static _ => PollingResult.CreateSuccess(),
            PollingResult.CreateTimeout,
            cancellationToken);

    private static async Task<TPollingResult> PollAsync<TResult, TPollingResult>(
        PollingOptions options,
        Func<CancellationToken, Task<TResult>>? func,
        Func<TResult, TPollingResult> onSuccess,
        Func<PollingError, TPollingResult> onTimeout,
        CancellationToken cancellationToken)
        where TResult : Result
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(func);
        var timeProvider = options.TimeProvider;
        ArgumentNullException.ThrowIfNull(timeProvider);

        using var timeoutCts = new CancellationTokenSource(options.Timeout, timeProvider);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        using var timer = new PeriodicTimer(options.TimerTick, timeProvider);

        List<Error> errors = [];

        try
        {
            do
            {
                TResult result;

                try
                {
                    result = await func(linkedCts.Token).ConfigureAwait(false);
                }
                catch (Exception exception) when (IsFailedAttempt(exception, options.TreatAsFailedAttempt, linkedCts.Token))
                {
                    errors.Add(new PollingExceptionError(exception));
                    continue;
                }

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

    // The poll's own cancellation, by its timeout or by the caller's token, decides how the poll ends, so it never
    // counts as a failed attempt.
    private static bool IsFailedAttempt(
        Exception exception,
        Func<Exception, bool>? treatAsFailedAttempt,
        CancellationToken pollToken)
        => treatAsFailedAttempt is not null
            && !(exception is OperationCanceledException && pollToken.IsCancellationRequested)
            && treatAsFailedAttempt(exception);

    // Both adapters pass a null func through, so the core rejects it with ArgumentNullException when the poll is
    // awaited, as it does for every other overload.
    private static Func<CancellationToken, Task<TResult>>? IgnoreToken<TResult>(Func<Task<TResult>>? func)
        => func is null ? null : _ => func();

    private static Func<CancellationToken, Task<Result>>? ToResult(Func<CancellationToken, Task<bool>>? func)
        => func is null
            ? null
            : async cancellationToken => await func(cancellationToken).ConfigureAwait(false)
                ? Result.Success()
                : Result.Failure(ConditionNotMet);
}
