using Vulthil.Framework.Results.Results;

namespace Vulthil.SharedKernel.Primitives;

public static partial class ResultExtensions
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
}

