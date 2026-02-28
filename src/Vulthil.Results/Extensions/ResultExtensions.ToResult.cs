namespace Vulthil.Results;

/// <summary>
/// Provides extension methods for <see cref="Result"/> and <see cref="Result{T}"/>.
/// </summary>
public static partial class ResultExtensions
{
    /// <summary>
    /// Converts a nullable value type to a <see cref="Result{T}"/>, returning a failure with the specified error when the value is <see langword="null"/>.
    /// </summary>
    public static Result<T> ToResult<T>(in this T? nullable, Error error)
        where T : struct
    {
        if (!nullable.HasValue)
        {
            return Result.Failure<T>(error);
        }

        return Result.Success(nullable.Value);
    }
    /// <summary>
    /// Converts a nullable reference type to a <see cref="Result{T}"/>, returning a failure with the specified error when the value is <see langword="null"/>.
    /// </summary>
    public static Result<T> ToResult<T>(this T? obj, Error error)
        where T : class
    {
        if (obj is null)
        {
            return Result.Failure<T>(error);
        }

        return Result.Success(obj);
    }

    /// <summary>
    /// Asynchronously converts a nullable value type to a <see cref="Result{T}"/>, returning a failure with the specified error when the value is <see langword="null"/>.
    /// </summary>
    public static async Task<Result<T>> ToResultAsync<T>(this Task<T?> nullableTask, Error errors)
        where T : struct
    {
        var nullable = await nullableTask.ConfigureAwait(false);
        return nullable.ToResult(errors);
    }

    /// <summary>
    /// Asynchronously converts a nullable reference type to a <see cref="Result{T}"/>, returning a failure with the specified error when the value is <see langword="null"/>.
    /// </summary>
    public static async Task<Result<T>> ToResultAsync<T>(this Task<T?> nullableTask, Error errors)
        where T : class
    {
        var nullable = await nullableTask.ConfigureAwait(false);
        return nullable.ToResult(errors);
    }
}
