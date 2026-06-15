namespace Vulthil.Results;

/// <summary>
/// Provides extension methods for <see cref="Result"/> and <see cref="Result{T}"/>.
/// </summary>
public static partial class ResultExtensions
{
    /// <summary>
    /// Returns the original result when it is successful; otherwise returns the supplied fallback result.
    /// </summary>
    public static Result Or(this Result result, Result fallback) =>
        result.IsSuccess ? result : fallback;
    /// <summary>
    /// Returns the original typed result when it is successful; otherwise returns the supplied fallback result.
    /// </summary>
    public static Result<T1> Or<T1>(this Result<T1> result, Result<T1> fallback) =>
        result.IsSuccess ? result : fallback;

    /// <inheritdoc cref="Or(Result, Result)"/>
    public static async Task<Result> OrAsync(this Task<Result> resultTask, Result fallback) =>
        (await resultTask).Or(fallback);
    /// <inheritdoc cref="Or{T1}(Result{T1}, Result{T1})"/>
    public static async Task<Result<T1>> OrAsync<T1>(this Task<Result<T1>> resultTask, Result<T1> fallback) =>
        (await resultTask).Or(fallback);

    /// <summary>
    /// Returns the original result when it is successful; otherwise invokes the fallback factory with the error to produce a replacement result.
    /// </summary>
    public static Result OrElse(this Result result, Func<Error, Result> fallback) =>
        result.IsSuccess ? result : fallback(result.Error);
    /// <summary>
    /// Returns the original typed result when it is successful; otherwise invokes the fallback factory with the error to produce a replacement result.
    /// </summary>
    public static Result<T1> OrElse<T1>(this Result<T1> result, Func<Error, Result<T1>> fallback) =>
        result.IsSuccess ? result : fallback(result.Error);

    /// <summary>
    /// Asynchronously returns the original result when it is successful; otherwise invokes the fallback factory with the error to produce a replacement result.
    /// </summary>
    public static async Task<Result> OrElseAsync(this Result result, Func<Error, Task<Result>> fallback) =>
        result.IsSuccess ? result : await fallback(result.Error);
    /// <inheritdoc cref="OrElseAsync(Result, Func{Error, Task{Result}})"/>
    public static async Task<Result> OrElseAsync(this Task<Result> resultTask, Func<Error, Result> fallback) =>
        (await resultTask).OrElse(fallback);
    /// <inheritdoc cref="OrElseAsync(Result, Func{Error, Task{Result}})"/>
    public static async Task<Result> OrElseAsync(this Task<Result> resultTask, Func<Error, Task<Result>> fallback) =>
        await (await resultTask).OrElseAsync(fallback);
    /// <summary>
    /// Asynchronously returns the original typed result when it is successful; otherwise invokes the fallback factory with the error to produce a replacement result.
    /// </summary>
    public static async Task<Result<T1>> OrElseAsync<T1>(this Result<T1> result, Func<Error, Task<Result<T1>>> fallback) =>
        result.IsSuccess ? result : await fallback(result.Error);
    /// <inheritdoc cref="OrElseAsync{T1}(Result{T1}, Func{Error, Task{Result{T1}}})"/>
    public static async Task<Result<T1>> OrElseAsync<T1>(this Task<Result<T1>> resultTask, Func<Error, Result<T1>> fallback) =>
        (await resultTask).OrElse(fallback);
    /// <inheritdoc cref="OrElseAsync{T1}(Result{T1}, Func{Error, Task{Result{T1}}})"/>
    public static async Task<Result<T1>> OrElseAsync<T1>(this Task<Result<T1>> resultTask, Func<Error, Task<Result<T1>>> fallback) =>
        await (await resultTask).OrElseAsync(fallback);
}
