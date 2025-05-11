using Vulthil.Framework.Results.Results;

namespace Vulthil.SharedKernel.Primitives;

public static partial class ResultExtensions
{
    public static Result Bind(this Result result, Func<Result> bind) =>
        result.IsSuccess ? bind() : result;
    public static Result Bind<T1>(this Result<T1> result, Func<T1, Result> bind) =>
        result.IsSuccess ? bind(result.Value) : result;
    public static Result<T2> Bind<T2>(this Result result, Func<Result<T2>> bind) =>
        result.IsSuccess ? bind() : Result.Failure<T2>(result.Error);
    public static Result<T2> Bind<T1, T2>(this Result<T1> result, Func<T1, Result<T2>> bind) =>
        result.IsSuccess ? bind(result.Value) : Result.Failure<T2>(result.Error);

    public static async Task<Result> BindAsync(this Result result, Func<Task<Result>> bind) =>
        result.IsSuccess ? await bind() : result;
    public static async Task<Result> BindAsync(this Task<Result> resultTask, Func<Result> bind) =>
        (await resultTask).Bind(bind);
    public static async Task<Result> BindAsync(this Task<Result> resultTask, Func<Task<Result>> bind) =>
        await (await resultTask).BindAsync(bind);
    public static async Task<Result> BindAsync<T1>(this Result<T1> result, Func<T1, Task<Result>> bind) =>
        result.IsSuccess ? await bind(result.Value) : result;
    public static async Task<Result> BindAsync<T1>(this Task<Result<T1>> resultTask, Func<T1, Result> bind) =>
        (await resultTask).Bind(bind);
    public static async Task<Result> BindAsync<T1>(this Task<Result<T1>> resultTask, Func<T1, Task<Result>> bind) =>
        await (await resultTask).BindAsync(bind);

    public static async Task<Result<T2>> BindAsync<T2>(this Result result, Func<Task<Result<T2>>> bind) =>
        result.IsSuccess ? await bind() : Result.Failure<T2>(result.Error);
    public static async Task<Result<T2>> BindAsync<T2>(this Task<Result> resultTask, Func<Result<T2>> bind) =>
        (await resultTask).Bind(bind);
    public static async Task<Result<T2>> BindAsync<T2>(this Task<Result> resultTask, Func<Task<Result<T2>>> bind) =>
        await (await resultTask).BindAsync(bind);

    public static async Task<Result<T2>> BindAsync<T1, T2>(this Result<T1> result, Func<T1, Task<Result<T2>>> bind) =>
        result.IsSuccess ? await bind(result.Value) : Result.Failure<T2>(result.Error);
    public static async Task<Result<T2>> BindAsync<T1, T2>(this Task<Result<T1>> resultTask, Func<T1, Result<T2>> bind) =>
        (await resultTask).Bind(bind);
    public static async Task<Result<T2>> BindAsync<T1, T2>(this Task<Result<T1>> resultTask, Func<T1, Task<Result<T2>>> bind) =>
        await (await resultTask).BindAsync(bind);
}
