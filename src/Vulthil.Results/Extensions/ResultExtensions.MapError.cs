namespace Vulthil.Results;

/// <summary>
/// Provides extension methods for <see cref="Result"/> and <see cref="Result{T}"/>.
/// </summary>
public static partial class ResultExtensions
{
    /// <summary>
    /// Transforms the error of a failed result using the provided mapping function; otherwise propagates the success.
    /// </summary>
    public static Result MapError(this Result result, Func<Error, Error> map) =>
        result.IsFailure ? Result.Failure(map(result.Error)) : result;
    /// <summary>
    /// Transforms the error of a failed typed result using the provided mapping function; otherwise propagates the success.
    /// </summary>
    public static Result<T1> MapError<T1>(this Result<T1> result, Func<Error, Error> map) =>
        result.IsFailure ? Result.Failure<T1>(map(result.Error)) : result;

    /// <summary>
    /// Asynchronously transforms the error of a failed result using the provided mapping function; otherwise propagates the success.
    /// </summary>
    public static async Task<Result> MapErrorAsync(this Result result, Func<Error, Task<Error>> map) =>
        result.IsFailure ? Result.Failure(await map(result.Error).ConfigureAwait(false)) : result;
    /// <inheritdoc cref="MapErrorAsync(Result, Func{Error, Task{Error}})"/>
    public static async Task<Result> MapErrorAsync(this Task<Result> resultTask, Func<Error, Error> map) =>
        (await resultTask.ConfigureAwait(false)).MapError(map);
    /// <inheritdoc cref="MapErrorAsync(Result, Func{Error, Task{Error}})"/>
    public static async Task<Result> MapErrorAsync(this Task<Result> resultTask, Func<Error, Task<Error>> map) =>
        await (await resultTask.ConfigureAwait(false)).MapErrorAsync(map).ConfigureAwait(false);
    /// <summary>
    /// Asynchronously transforms the error of a failed typed result using the provided mapping function; otherwise propagates the success.
    /// </summary>
    public static async Task<Result<T1>> MapErrorAsync<T1>(this Result<T1> result, Func<Error, Task<Error>> map) =>
        result.IsFailure ? Result.Failure<T1>(await map(result.Error).ConfigureAwait(false)) : result;
    /// <inheritdoc cref="MapErrorAsync{T1}(Result{T1}, Func{Error, Task{Error}})"/>
    public static async Task<Result<T1>> MapErrorAsync<T1>(this Task<Result<T1>> resultTask, Func<Error, Error> map) =>
        (await resultTask.ConfigureAwait(false)).MapError(map);
    /// <inheritdoc cref="MapErrorAsync{T1}(Result{T1}, Func{Error, Task{Error}})"/>
    public static async Task<Result<T1>> MapErrorAsync<T1>(this Task<Result<T1>> resultTask, Func<Error, Task<Error>> map) =>
        await (await resultTask.ConfigureAwait(false)).MapErrorAsync(map).ConfigureAwait(false);
}
