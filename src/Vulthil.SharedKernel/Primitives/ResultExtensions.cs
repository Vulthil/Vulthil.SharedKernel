using System.Threading.Tasks;

namespace Vulthil.SharedKernel.Primitives;

public static class ResultExtensions
{
    public static Result<T2> Map<T2>(this Result result, Func<T2> map) =>
        result.IsSuccess ? Result.Success(map()) : Result.Failure<T2>(result.Error);
    public static async Task<Result<T2>> MapAsync<T2>(this Result result, Func<Task<T2>> map) =>
        result.IsSuccess ? Result.Success(await map()) : Result.Failure<T2>(result.Error);
    public static async Task<Result<T2>> MapAsync<T2>(this Task<Result> resultTask, Func<T2> map)
    {
        var result = await resultTask;
        return result.Map(map);
    }
    public static async Task<Result<T2>> MapAsync<T2>(this Task<Result> resultTask, Func<Task<T2>> map)
    {
        var result = await resultTask;
        return await result.MapAsync(map);
    }

    public static Result<T2> Map<T1, T2>(this Result<T1> result, Func<T1, T2> map) =>
        result.IsSuccess ? Result.Success(map(result.Value)) : Result.Failure<T2>(result.Error);
    public static async Task<Result<T2>> MapAsync<T1, T2>(this Result<T1> result, Func<T1, Task<T2>> map) => 
        result.IsSuccess ? Result.Success(await map(result.Value)) : Result.Failure<T2>(result.Error);
    public static async Task<Result<T2>> MapAsync<T1, T2>(this Task<Result<T1>> resultTask, Func<T1, T2> map)
    {
        var result = await resultTask;
        return result.Map(map);
    }
    public static async Task<Result<T2>> MapAsync<T1, T2>(this Task<Result<T1>> resultTask, Func<T1, Task<T2>> map)
    {
        var result = await resultTask;
        return await result.MapAsync(map);
    }

    public static Result Bind(this Result result, Func<Result> bind) =>
        result.IsSuccess ? bind() : result;
    public static async Task<Result> BindAsync(this Result result, Func<Task<Result>> bind)
    {
        return result.IsSuccess ? await bind() : result;
    }
    public static async Task<Result> BindAsync(this Task<Result> resultTask, Func<Result> bind)
    {
        var result = await resultTask;
        return result.Bind(bind);
    }
    public static async Task<Result> BindAsync(this Task<Result> resultTask, Func<Task<Result>> bind)
    {
        var result = await resultTask;
        return await result.BindAsync(bind);
    }

    public static Result Bind<T1>(this Result<T1> result, Func<T1, Result> bind) =>
        result.IsSuccess ? bind(result.Value) : result;
    public static Result<T2> Bind<T2>(this Result result, Func<Result<T2>> bind) =>
        result.IsSuccess ? bind() : Result.Failure<T2>(result.Error);
    public static Result<T2> Bind<T1, T2>(this Result<T1> result, Func<T1, Result<T2>> bind) =>
        result.IsSuccess ? bind(result.Value) : Result.Failure<T2>(result.Error);

    

    public static Task<Result<T2>> BindAsync<T1, T2>(this Result<T1> result, Func<T1, Task<Result<T2>>> bind) =>
        result.IsSuccess ? bind(result.Value) : Task.FromResult(Result.Failure<T2>(result.Error));
    public static async Task<Result<T2>> BindAsync<T1, T2>(this Task<Result<T1>> resultTask, Func<T1, Result<T2>> bind)
    {
        var result = await resultTask;
        return result.Bind(bind);
    }
    public static async Task<Result<T2>> BindAsync<T1, T2>(this Task<Result<T1>> resultTask, Func<T1, Task<Result<T2>>> bind)
    {
        var result = await resultTask;
        return await result.BindAsync(bind);
    }

    public static TOut Match<TOut>(
        this Result result,
        Func<TOut> onSuccess,
        Func<Error, TOut> onFailure)
    {
        return result.IsSuccess ? onSuccess() : onFailure(result.Error);
    }
    public static async Task<TOut> MatchAsync<TOut>(
        this Result result,
        Func<Task<TOut>> onSuccess,
        Func<Error, Task<TOut>> onFailure)
    {
        return result.IsSuccess ? await onSuccess() : await onFailure(result.Error);
    }

    public static async Task<TOut> MatchAsync<TOut>(
        this Task<Result> resultTask,
        Func<Task<TOut>> onSuccess,
        Func<Error, Task<TOut>> onFailure)
    {
        var result = await resultTask;
        return result.IsSuccess ? await onSuccess() : await onFailure(result.Error);
    }

    public static TOut Match<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, TOut> onSuccess,
        Func<Error, TOut> onFailure)
    {
        return result.IsSuccess ? onSuccess(result.Value) : onFailure(result.Error);
    }
    public static async Task<TOut> MatchAsync<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, TOut> onSuccess,
        Func<Error, Task<TOut>> onFailure)
    {
        return result.IsSuccess ? onSuccess(result.Value) : await onFailure(result.Error);
    }
    public static async Task<TOut> MatchAsync<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, Task<TOut>> onSuccess,
        Func<Error, TOut> onFailure)
    {
        return result.IsSuccess ? await onSuccess(result.Value) : onFailure(result.Error);
    }
    public static async Task<TOut> MatchAsync<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, Task<TOut>> onSuccess,
        Func<Error, Task<TOut>> onFailure)
    {
        return result.IsSuccess ? await onSuccess(result.Value) : await onFailure(result.Error);
    }
    public static async Task<TOut> MatchAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Task<TOut>> onSuccess,
        Func<Error, Task<TOut>> onFailure)
    {
        var result = await resultTask;
        return await result.MatchAsync(onSuccess, onFailure);
    }
}

