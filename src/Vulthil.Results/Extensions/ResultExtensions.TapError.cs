namespace Vulthil.Results;

/// <summary>
/// Provides extension methods for <see cref="Result"/> and <see cref="Result{T}"/>.
/// </summary>
public static partial class ResultExtensions
{
    /// <summary>
    /// Executes a side-effect action with the error when the result is a failure and returns the original result unchanged.
    /// </summary>
    public static Result TapError(this Result result, Action<Error> action)
    {
        if (result.IsFailure)
        {
            action(result.Error);
        }

        return result;
    }
    /// <summary>
    /// Executes a side-effect action with the error when the typed result is a failure and returns the original result unchanged.
    /// </summary>
    public static Result<T1> TapError<T1>(this Result<T1> result, Action<Error> action)
    {
        if (result.IsFailure)
        {
            action(result.Error);
        }

        return result;
    }

    /// <summary>
    /// Asynchronously executes a side-effect with the error when the result is a failure and returns the original result unchanged.
    /// </summary>
    public static async Task<Result> TapErrorAsync(this Result result, Func<Error, Task> action)
    {
        if (result.IsFailure)
        {
            await action(result.Error);
        }

        return result;
    }
    /// <inheritdoc cref="TapErrorAsync(Result, Func{Error, Task})"/>
    public static async Task<Result> TapErrorAsync(this Task<Result> resultTask, Action<Error> action) =>
        (await resultTask).TapError(action);
    /// <inheritdoc cref="TapErrorAsync(Result, Func{Error, Task})"/>
    public static async Task<Result> TapErrorAsync(this Task<Result> resultTask, Func<Error, Task> action) =>
        await (await resultTask).TapErrorAsync(action);
    /// <summary>
    /// Asynchronously executes a side-effect with the error when the typed result is a failure and returns the original result unchanged.
    /// </summary>
    public static async Task<Result<T1>> TapErrorAsync<T1>(this Result<T1> result, Func<Error, Task> action)
    {
        if (result.IsFailure)
        {
            await action(result.Error);
        }

        return result;
    }
    /// <inheritdoc cref="TapErrorAsync{T1}(Result{T1}, Func{Error, Task})"/>
    public static async Task<Result<T1>> TapErrorAsync<T1>(this Task<Result<T1>> resultTask, Action<Error> action) =>
        (await resultTask).TapError(action);
    /// <inheritdoc cref="TapErrorAsync{T1}(Result{T1}, Func{Error, Task})"/>
    public static async Task<Result<T1>> TapErrorAsync<T1>(this Task<Result<T1>> resultTask, Func<Error, Task> action) =>
        await (await resultTask).TapErrorAsync(action);
}
