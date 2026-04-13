using Vulthil.Results;

namespace Vulthil.Extensions.Testing;

/// <summary>
/// Represents the result of a polling operation.
/// Inherits from <see cref="Result"/> and provides strongly-typed access to the
/// <see cref="Extensions.Testing.PollingError"/> when the operation times out.
/// </summary>
/// <remarks>
/// Use the <see cref="PollingError"/> property to inspect individual errors from each
/// polling attempt when the operation fails:
/// <code>
/// PollingResult result = await Polling.WaitAsync(timeout, myFunc);
/// if (result.IsFailure)
/// {
///     foreach (var error in result.PollingError!.Errors)
///     {
///         // inspect each attempt's error
///     }
/// }
/// </code>
/// </remarks>
public sealed class PollingResult : Result
{
    /// <summary>
    /// Gets the <see cref="Extensions.Testing.PollingError"/> containing all errors collected during polling,
    /// or <see langword="null"/> when the polling operation succeeded.
    /// </summary>
    public PollingError? PollingError { get; }

    private PollingResult()
        : base(true, Error.None) { }

    private PollingResult(PollingError error)
        : base(false, error) => PollingError = error;

    internal static PollingResult CreateSuccess() => new();

    internal static PollingResult CreateTimeout(PollingError error) => new(error);
}

/// <summary>
/// Represents the result of a polling operation that returns a value of type <typeparamref name="T"/>.
/// Inherits from <see cref="Result{T}"/> and provides strongly-typed access to the
/// <see cref="Extensions.Testing.PollingError"/> when the operation times out.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
/// <remarks>
/// Use the <see cref="PollingError"/> property to inspect individual errors from each
/// polling attempt when the operation fails:
/// <code>
/// PollingResult&lt;MyDto&gt; result = await Polling.WaitAsync&lt;MyDto&gt;(timeout, myFunc);
/// if (result.IsFailure)
/// {
///     foreach (var error in result.PollingError!.Errors)
///     {
///         // inspect each attempt's error
///     }
/// }
/// </code>
/// </remarks>
public sealed class PollingResult<T> : Result<T>
{
    /// <summary>
    /// Gets the <see cref="Extensions.Testing.PollingError"/> containing all errors collected during polling,
    /// or <see langword="null"/> when the polling operation succeeded.
    /// </summary>
    public PollingError? PollingError { get; }

    private PollingResult(T value)
        : base(value, true, Error.None) { }

    private PollingResult(PollingError error)
        : base(default, false, error) => PollingError = error;

    internal static PollingResult<T> CreateSuccess(T value) => new(value);

    internal static PollingResult<T> CreateTimeout(PollingError error) => new(error);
}
