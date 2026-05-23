using Vulthil.Results;

namespace Vulthil.Extensions.Testing;

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
