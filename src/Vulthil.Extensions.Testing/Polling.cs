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
    {
        return WaitAsync(timeout, func, TimeSpan.FromSeconds(1), CancellationToken.None);
    }

    /// <summary>
    /// Polls the provided function at one-second intervals until it returns a successful result or the timeout expires.
    /// </summary>
    /// <typeparam name="T">The type of the expected value.</typeparam>
    /// <param name="timeout">The maximum duration to poll.</param>
    /// <param name="func">The function to invoke each tick.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    /// A <see cref="PollingResult{T}"/> containing the first successful result,
    /// or a <see cref="PollingError"/> with all errors collected during polling.
    /// </returns>
    public static Task<PollingResult<T>> WaitAsync<T>(
        TimeSpan timeout,
        Func<Task<Result<T>>> func,
        CancellationToken cancellationToken)
    {
        return WaitAsync(timeout, func, TimeSpan.FromSeconds(1), cancellationToken);
    }

    /// <summary>
    /// Polls the provided function at regular intervals until it returns a successful result or the timeout expires.
    /// </summary>
    /// <typeparam name="T">The type of the expected value.</typeparam>
    /// <param name="timeout">The maximum duration to poll.</param>
    /// <param name="func">The function to invoke each tick.</param>
    /// <param name="timerTick">The interval between polls.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    /// A <see cref="PollingResult{T}"/> containing the first successful result,
    /// or a <see cref="PollingError"/> with all errors collected during polling.
    /// </returns>
    public static async Task<PollingResult<T>> WaitAsync<T>(
        TimeSpan timeout,
        Func<Task<Result<T>>> func,
        TimeSpan timerTick,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(timerTick);
        List<Error> errors = [];

        DateTime endTimeUtc = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < endTimeUtc &&
               await timer.WaitForNextTickAsync(cancellationToken))
        {
            Result<T> result = await func();

            if (result.IsSuccess)
            {
                return PollingResult<T>.CreateSuccess(result.Value);
            }

            errors.Add(result.Error);
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
    {
        return WaitAsync(timeout, func, TimeSpan.FromSeconds(1), CancellationToken.None);
    }

    /// <summary>
    /// Polls the provided function at one-second intervals until it returns a successful result or the timeout expires.
    /// </summary>
    /// <param name="timeout">The maximum duration to poll.</param>
    /// <param name="func">The function to invoke each tick.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    /// A <see cref="PollingResult"/> containing a success indication,
    /// or a <see cref="PollingError"/> with all errors collected during polling.
    /// </returns>
    public static Task<PollingResult> WaitAsync(
        TimeSpan timeout,
        Func<Task<Result>> func,
        CancellationToken cancellationToken)
    {
        return WaitAsync(timeout, func, TimeSpan.FromSeconds(1), cancellationToken);
    }

    /// <summary>
    /// Polls the provided function at regular intervals until it returns a successful result or the timeout expires.
    /// </summary>
    /// <param name="timeout">The maximum duration to poll.</param>
    /// <param name="func">The function to invoke each tick.</param>
    /// <param name="timerTick">The interval between polls.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    /// A <see cref="PollingResult"/> containing a success indication,
    /// or a <see cref="PollingError"/> with all errors collected during polling.
    /// </returns>
    public static async Task<PollingResult> WaitAsync(
        TimeSpan timeout,
        Func<Task<Result>> func,
        TimeSpan timerTick,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(timerTick);
        List<Error> errors = [];

        DateTime endTimeUtc = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < endTimeUtc &&
               await timer.WaitForNextTickAsync(cancellationToken))
        {
            Result result = await func();

            if (result.IsSuccess)
            {
                return PollingResult.CreateSuccess();
            }

            errors.Add(result.Error);
        }

        return PollingResult.CreateTimeout(PollingError.FromErrors(errors));
    }
}

/// <summary>
/// Represents a polling timeout error that aggregates all individual errors
/// collected from each failed polling attempt.
/// </summary>
/// <remarks>
/// When a <see cref="Polling.WaitAsync{T}(TimeSpan, Func{Task{Result{T}}}, TimeSpan, CancellationToken)"/>
/// call times out, the returned <see cref="PollingResult{T}"/> provides the
/// <see cref="PollingError"/> via <see cref="PollingResult{T}.PollingError"/>:
/// <code>
/// var result = await Polling.WaitAsync(timeout, myFunc);
/// if (result.IsFailure &amp;&amp; result.PollingError is { } pollingError)
/// {
///     foreach (var error in pollingError.Errors)
///     {
///         // inspect each attempt's error
///     }
/// }
/// </code>
/// </remarks>
public sealed record PollingError : Error
{
    /// <summary>
    /// Gets the individual errors collected during polling.
    /// Each element represents the error from a single failed attempt.
    /// </summary>
    public Error[] Errors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PollingError"/> record.
    /// </summary>
    /// <param name="errors">The individual errors from each polling attempt.</param>
    public PollingError(Error[] errors)
        : base(Polling.Timeout.Code,
              Polling.Timeout.Description,
              ErrorType.Failure) => Errors = errors;

    /// <summary>
    /// Creates a <see cref="PollingError"/> from a collection of errors.
    /// </summary>
    /// <param name="errors">The errors collected during polling.</param>
    /// <returns>A <see cref="PollingError"/> containing all provided errors.</returns>
    public static PollingError FromErrors(IEnumerable<Error> errors) =>
        new([.. errors]);
}
