namespace Vulthil.Results;

/// <summary>
/// Provides extension methods for <see cref="Result"/> and <see cref="Result{T}"/>.
/// </summary>
public static partial class ResultExtensions
{
    /// <summary>
    /// Chains into a subsequent operation when the result is successful; otherwise propagates the failure.
    /// </summary>
    public static Result Bind(this Result result, Func<Result> bind) =>
        result.IsSuccess ? bind() : result;
    /// <summary>
    /// Chains the success value into a subsequent operation; otherwise propagates the failure.
    /// </summary>
    public static Result Bind<T1>(this Result<T1> result, Func<T1, Result> bind) =>
        result.IsSuccess ? bind(result.Value) : result;
    /// <summary>
    /// Chains into a subsequent operation that returns a typed result; otherwise propagates the failure.
    /// </summary>
    public static Result<T2> Bind<T2>(this Result result, Func<Result<T2>> bind) =>
        result.IsSuccess ? bind() : Result.Failure<T2>(result.Error);
    /// <summary>
    /// Chains the success value into a subsequent operation that returns a typed result; otherwise propagates the failure.
    /// </summary>
    public static Result<T2> Bind<T1, T2>(this Result<T1> result, Func<T1, Result<T2>> bind) =>
        result.IsSuccess ? bind(result.Value) : Result.Failure<T2>(result.Error);

    /// <summary>
    /// Asynchronously chains into a subsequent operation when the result is successful; otherwise propagates the failure.
    /// </summary>
    public static async Task<Result> BindAsync(this Result result, Func<Task<Result>> bind) =>
        result.IsSuccess ? await bind() : result;
    /// <inheritdoc cref="BindAsync(Result, Func{Task{Result}})"/>
    public static async Task<Result> BindAsync(this Task<Result> resultTask, Func<Result> bind) =>
        (await resultTask).Bind(bind);
    /// <inheritdoc cref="BindAsync(Result, Func{Task{Result}})"/>
    public static async Task<Result> BindAsync(this Task<Result> resultTask, Func<Task<Result>> bind) =>
        await (await resultTask).BindAsync(bind);
    /// <summary>
    /// Asynchronously chains the success value into a subsequent operation; otherwise propagates the failure.
    /// </summary>
    public static async Task<Result> BindAsync<T1>(this Result<T1> result, Func<T1, Task<Result>> bind) =>
        result.IsSuccess ? await bind(result.Value) : result;
    /// <inheritdoc cref="BindAsync{T1}(Result{T1}, Func{T1, Task{Result}})"/>
    public static async Task<Result> BindAsync<T1>(this Task<Result<T1>> resultTask, Func<T1, Result> bind) =>
        (await resultTask).Bind(bind);
    /// <inheritdoc cref="BindAsync{T1}(Result{T1}, Func{T1, Task{Result}})"/>
    public static async Task<Result> BindAsync<T1>(this Task<Result<T1>> resultTask, Func<T1, Task<Result>> bind) =>
        await (await resultTask).BindAsync(bind);

    /// <summary>
    /// Asynchronously chains into a subsequent operation that returns a typed result; otherwise propagates the failure.
    /// </summary>
    public static async Task<Result<T2>> BindAsync<T2>(this Result result, Func<Task<Result<T2>>> bind) =>
        result.IsSuccess ? await bind() : Result.Failure<T2>(result.Error);
    /// <inheritdoc cref="BindAsync{T2}(Result, Func{Task{Result{T2}}})"/>
    public static async Task<Result<T2>> BindAsync<T2>(this Task<Result> resultTask, Func<Result<T2>> bind) =>
        (await resultTask).Bind(bind);
    /// <inheritdoc cref="BindAsync{T2}(Result, Func{Task{Result{T2}}})"/>
    public static async Task<Result<T2>> BindAsync<T2>(this Task<Result> resultTask, Func<Task<Result<T2>>> bind) =>
        await (await resultTask).BindAsync(bind);

    /// <summary>
    /// Asynchronously chains the success value into a subsequent operation that returns a typed result; otherwise propagates the failure.
    /// </summary>
    public static async Task<Result<T2>> BindAsync<T1, T2>(this Result<T1> result, Func<T1, Task<Result<T2>>> bind) =>
        result.IsSuccess ? await bind(result.Value) : Result.Failure<T2>(result.Error);
    /// <inheritdoc cref="BindAsync{T1, T2}(Result{T1}, Func{T1, Task{Result{T2}}})"/>
    public static async Task<Result<T2>> BindAsync<T1, T2>(this Task<Result<T1>> resultTask, Func<T1, Result<T2>> bind) =>
        (await resultTask).Bind(bind);
    /// <inheritdoc cref="BindAsync{T1, T2}(Result{T1}, Func{T1, Task{Result{T2}}})"/>
    public static async Task<Result<T2>> BindAsync<T1, T2>(this Task<Result<T1>> resultTask, Func<T1, Task<Result<T2>>> bind) =>
        await (await resultTask).BindAsync(bind);
}
