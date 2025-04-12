namespace Vulthil.SharedKernel.Primitives;

public static class ResultExtensions
{
    public static Result<T2> Map<T1, T2>(this Result<T1> result, Func<T1, T2> map) =>
        result.IsSuccess ? Result.Success(map(result.Value)) : Result.Failure<T2>(result.Error);
    public static async Task<Result<T2>> MapAsync<T1, T2>(this Task<Result<T1>> resultTask, Func<T1, Task<T2>> map)
    {
        var result = await resultTask;
        return result.IsSuccess ? Result.Success(await map(result.Value)) : Result.Failure<T2>(result.Error);
    }

    public static Result<T2> Bind<T1, T2>(this Result<T1> result, Func<T1, Result<T2>> bind) =>
        result.IsSuccess ? bind(result.Value) : Result.Failure<T2>(result.Error);
    public static async Task<Result<T2>> BindAsync<T1, T2>(this Task<Result<T1>> resultTask, Func<T1, Task<Result<T2>>> bind)
    {
        var result = await resultTask;
        return result.IsSuccess ? await bind(result.Value) : Result.Failure<T2>(result.Error);
    }

    public static TOut Match<TOut>(
        this Result result,
        Func<TOut> onSuccess,
        Func<Error, TOut> onFailure)
    {
        return result.IsSuccess ? onSuccess() : onFailure(result.Error);
    }

    public static TOut Match<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, TOut> onSuccess,
        Func<Error, TOut> onFailure)
    {
        return result.IsSuccess ? onSuccess(result.Value) : onFailure(result.Error);
    }
}

