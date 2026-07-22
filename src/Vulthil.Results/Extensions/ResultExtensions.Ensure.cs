namespace Vulthil.Results;

/// <summary>
/// Provides extension methods for <see cref="Result"/> and <see cref="Result{T}"/>.
/// </summary>
public static partial class ResultExtensions
{
    /// <summary>
    /// Verifies that a successful result satisfies the predicate; otherwise returns a failure with the specified error. Failures propagate unchanged.
    /// </summary>
    public static Result<T1> Ensure<T1>(this Result<T1> result, Func<T1, bool> predicate, Error error)
    {
        if (result.IsFailure)
        {
            return result;
        }

        return predicate(result.Value) ? result : Result.Failure<T1>(error);
    }

    /// <summary>
    /// Asynchronously verifies that a successful result satisfies the predicate; otherwise returns a failure with the specified error. Failures propagate unchanged.
    /// </summary>
    public static async Task<Result<T1>> EnsureAsync<T1>(this Result<T1> result, Func<T1, Task<bool>> predicate, Error error)
    {
        if (result.IsFailure)
        {
            return result;
        }

        return await predicate(result.Value).ConfigureAwait(false) ? result : Result.Failure<T1>(error);
    }
    /// <inheritdoc cref="Ensure{T1}(Result{T1}, Func{T1, bool}, Error)"/>
    public static async Task<Result<T1>> EnsureAsync<T1>(this Task<Result<T1>> resultTask, Func<T1, bool> predicate, Error error) =>
        (await resultTask.ConfigureAwait(false)).Ensure(predicate, error);
    /// <inheritdoc cref="EnsureAsync{T1}(Result{T1}, Func{T1, Task{bool}}, Error)"/>
    public static async Task<Result<T1>> EnsureAsync<T1>(this Task<Result<T1>> resultTask, Func<T1, Task<bool>> predicate, Error error) =>
        await (await resultTask.ConfigureAwait(false)).EnsureAsync(predicate, error).ConfigureAwait(false);
}
