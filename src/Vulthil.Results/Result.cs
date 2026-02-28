using System.Diagnostics.CodeAnalysis;

namespace Vulthil.Results;
/// <summary>
/// Represents the outcome of an operation that may succeed or fail.
/// </summary>
public class Result
{
    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }
    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;
    /// <summary>
    /// Gets the error associated with a failed result.
    /// Returns <see cref="Error.None"/> when <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    public Error Error { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Result"/> class.
    /// </summary>
    /// <param name="isSuccess">Whether the result represents success.</param>
    /// <param name="error">The error, or <see cref="Error.None"/> for success.</param>
    /// <exception cref="ArgumentException">Thrown when the success state and error are inconsistent.</exception>
    protected internal Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None ||
            !isSuccess && error == Error.None)
        {
            throw new ArgumentException("Invalid error", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>A successful <see cref="Result"/>.</returns>
    public static Result Success() => new(true, Error.None);
    /// <summary>
    /// Creates a successful result containing the specified value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The success value.</param>
    /// <returns>A successful <see cref="Result{TValue}"/>.</returns>
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    /// <param name="error">The error describing the failure.</param>
    /// <returns>A failed <see cref="Result"/>.</returns>
    public static Result Failure(Error error) => new(false, error);
    /// <summary>
    /// Creates a failed result of the specified value type.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="error">The error describing the failure.</param>
    /// <returns>A failed <see cref="Result{TValue}"/>.</returns>
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

    /// <summary>
    /// Creates a failed result from a validation error.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="error">The validation error.</param>
    /// <returns>A failed <see cref="Result{TValue}"/>.</returns>
    public static Result<TValue> ValidationFailure<TValue>(ValidationError error) => Failure<TValue>(error);
}

/// <summary>
/// Represents the outcome of an operation that returns a value of type <typeparamref name="TValue"/> on success.
/// </summary>
/// <typeparam name="TValue">The type of the success value.</typeparam>
public class Result<TValue> : Result
{
#pragma warning disable IDE0032 // Use auto property
    private readonly TValue? _value;
#pragma warning restore IDE0032

    /// <summary>
    /// Gets the value of a successful result.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when accessing the value of a failed result.</exception>
    [NotNull]
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failure result can't be accessed.");

    /// <summary>
    /// Initializes a new instance of the <see cref="Result{TValue}"/> class.
    /// </summary>
    /// <param name="value">The success value, or <see langword="default"/> for failures.</param>
    /// <param name="isSuccess">Whether the result represents success.</param>
    /// <param name="error">The error, or <see cref="Error.None"/> for success.</param>
    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error) => _value = value;

    /// <summary>
    /// Implicitly converts a value to a successful result, or a failed result if the value is <see langword="null"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    public static implicit operator Result<TValue>(TValue? value) =>
        value is not null ? Success(value) : Failure<TValue>(Error.NullValue);

}

