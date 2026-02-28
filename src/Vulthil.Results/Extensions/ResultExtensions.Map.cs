namespace Vulthil.Results;

/// <summary>
/// Provides extension methods for <see cref="Result"/> and <see cref="Result{T}"/>.
/// </summary>
public static partial class ResultExtensions
{
    /// <summary>
    /// Transforms a successful result into a new typed result using the provided mapping function; otherwise propagates the failure.
    /// </summary>
    public static Result<T2> Map<T2>(this Result result, Func<T2> map) =>
        result.IsSuccess ? Result.Success(map()) : Result.Failure<T2>(result.Error);
    /// <summary>
    /// Transforms the value of a successful result using the provided mapping function; otherwise propagates the failure.
    /// </summary>
    public static Result<T2> Map<T1, T2>(this Result<T1> result, Func<T1, T2> map) =>
        result.IsSuccess ? Result.Success(map(result.Value)) : Result.Failure<T2>(result.Error);

    /// <summary>
    /// Asynchronously transforms a successful result using the provided mapping function; otherwise propagates the failure.
    /// </summary>
    public static async Task<Result<T2>> MapAsync<T2>(this Result result, Func<Task<T2>> map) =>
        result.IsSuccess ? Result.Success(await map()) : Result.Failure<T2>(result.Error);
    /// <inheritdoc cref="MapAsync{T2}(Result, Func{Task{T2}})"/>
    public static async Task<Result<T2>> MapAsync<T2>(this Task<Result> resultTask, Func<T2> map) =>
         (await resultTask).Map(map);
    /// <inheritdoc cref="MapAsync{T2}(Result, Func{Task{T2}})"/>
    public static async Task<Result<T2>> MapAsync<T2>(this Task<Result> resultTask, Func<Task<T2>> map) =>
         await (await resultTask).MapAsync(map);
    /// <summary>
    /// Asynchronously transforms the value of a successful result using the provided mapping function; otherwise propagates the failure.
    /// </summary>
    public static async Task<Result<T2>> MapAsync<T1, T2>(this Result<T1> result, Func<T1, Task<T2>> map) =>
        result.IsSuccess ? Result.Success(await map(result.Value)) : Result.Failure<T2>(result.Error);
    /// <inheritdoc cref="MapAsync{T1, T2}(Result{T1}, Func{T1, Task{T2}})"/>
    public static async Task<Result<T2>> MapAsync<T1, T2>(this Task<Result<T1>> resultTask, Func<T1, T2> map) =>
        (await resultTask).Map(map);
    /// <inheritdoc cref="MapAsync{T1, T2}(Result{T1}, Func{T1, Task{T2}})"/>
    public static async Task<Result<T2>> MapAsync<T1, T2>(this Task<Result<T1>> resultTask, Func<T1, Task<T2>> map) =>
        await (await resultTask).MapAsync(map);
}

