namespace Vulthil.Results;

/// <summary>
/// Provides extension methods for <see cref="Result"/> and <see cref="Result{T}"/>.
/// </summary>
public static partial class ResultExtensions
{
    /// <summary>
    /// Executes a side-effect action when the result is successful and returns the original result unchanged.
    /// </summary>
    public static Result Tap(this Result result, Action action)
    {
        if (result.IsSuccess)
        {
            action();
        }

        return result;
    }
    /// <summary>
    /// Executes a side-effect action when the result is successful and returns the original result unchanged.
    /// </summary>
    public static Result<T1> Tap<T1>(this Result<T1> result, Action action)
    {
        if (result.IsSuccess)
        {
            action();
        }

        return result;
    }
    /// <summary>
    /// Executes a side-effect action with the success value and returns the original result unchanged.
    /// </summary>
    public static Result<T1> Tap<T1>(this Result<T1> result, Action<T1> action)
    {
        if (result.IsSuccess)
        {
            action(result.Value);
        }

        return result;
    }

    /// <summary>
    /// Asynchronously executes a side-effect when the result is successful and returns the original result unchanged.
    /// </summary>
    public static async Task<Result> TapAsync(this Result result, Func<Task> action)
    {
        if (result.IsSuccess)
        {
            await action();
        }

        return result;
    }
    /// <inheritdoc cref="TapAsync(Result, Func{Task})"/>
    public static async Task<Result> TapAsync(this Task<Result> resultTask, Action action) =>
        (await resultTask).Tap(action);
    /// <inheritdoc cref="TapAsync(Result, Func{Task})"/>
    public static async Task<Result> TapAsync(this Task<Result> resultTask, Func<Task> action) =>
        await (await resultTask).TapAsync(action);
    /// <summary>
    /// Asynchronously executes a side-effect when the typed result is successful and returns the original result unchanged.
    /// </summary>
    public static async Task<Result<T1>> TapAsync<T1>(this Result<T1> result, Func<Task> action)
    {
        if (result.IsSuccess)
        {
            await action();
        }

        return result;
    }
    /// <summary>
    /// Asynchronously executes a side-effect with the success value and returns the original result unchanged.
    /// </summary>
    public static async Task<Result<T1>> TapAsync<T1>(this Result<T1> result, Func<T1, Task> action)
    {
        if (result.IsSuccess)
        {
            await action(result.Value);
        }

        return result;
    }

    /// <inheritdoc cref="TapAsync{T1}(Result{T1}, Func{Task})"/>
    public static async Task<Result<T1>> TapAsync<T1>(this Task<Result<T1>> resultTask, Action action) =>
         (await resultTask).Tap(action);
    /// <inheritdoc cref="TapAsync{T1}(Result{T1}, Func{Task})"/>
    public static async Task<Result<T1>> TapAsync<T1>(this Task<Result<T1>> resultTask, Func<Task> action) =>
        await (await resultTask).TapAsync(action);
    /// <inheritdoc cref="TapAsync{T1}(Result{T1}, Func{T1, Task})"/>
    public static async Task<Result<T1>> TapAsync<T1>(this Task<Result<T1>> resultTask, Action<T1> action) =>
        (await resultTask).Tap(action);
    /// <inheritdoc cref="TapAsync{T1}(Result{T1}, Func{T1, Task})"/>
    public static async Task<Result<T1>> TapAsync<T1>(this Task<Result<T1>> resultTask, Func<T1, Task> action) =>
        await (await resultTask).TapAsync(action);
}
