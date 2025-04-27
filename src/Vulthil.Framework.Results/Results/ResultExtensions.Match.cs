using Vulthil.Framework.Results;
using Vulthil.Framework.Results.Results;

namespace Vulthil.SharedKernel.Primitives;

public static partial class ResultExtensions
{
    public static void Match(this Result result, Action onSuccess, Action<Error> onFailure)
    {
        if (result.IsSuccess)
        {
            onSuccess();
            return;
        }

        onFailure(result.Error);
    }
    public static void Match<T1>(this Result<T1> result, Action<T1> onSuccess, Action<Error> onFailure)
    {
        if (result.IsSuccess)
        {
            onSuccess(result.Value);
            return;
        }

        onFailure(result.Error);
    }
    public static TOut Match<TOut>(this Result result, Func<TOut> onSuccess, Func<Error, TOut> onFailure)
        => result.IsSuccess ? onSuccess() : onFailure(result.Error);
    public static TOut Match<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> onSuccess, Func<Error, TOut> onFailure)
        => result.IsSuccess ? onSuccess(result.Value) : onFailure(result.Error);

    public static async Task MatchAsync(this Result result, Func<Task> onSuccess, Action<Error> onFailure)
    {
        if (result.IsSuccess)
        {
            await onSuccess();
            return;
        }

        onFailure(result.Error);
    }
    public static async Task MatchAsync(this Result result, Action onSuccess, Func<Error, Task> onFailure)
    {
        if (result.IsSuccess)
        {
            onSuccess();
            return;
        }

        await onFailure(result.Error);
    }
    public static async Task MatchAsync(this Result result, Func<Task> onSuccess, Func<Error, Task> onFailure)
    {
        if (result.IsSuccess)
        {
            await onSuccess();
            return;
        }

        await onFailure(result.Error);
    }
    public static async Task MatchAsync(this Task<Result> resultTask, Action onSuccess, Action<Error> onFailure)
    {
        var result = await resultTask;
        result.Match(onSuccess, onFailure);
    }
    public static async Task MatchAsync(this Task<Result> resultTask, Func<Task> onSuccess, Action<Error> onFailure)
    {
        var result = await resultTask;
        await result.MatchAsync(onSuccess, onFailure);
    }
    public static async Task MatchAsync(this Task<Result> resultTask, Action onSuccess, Func<Error, Task> onFailure)
    {
        var result = await resultTask;
        await result.MatchAsync(onSuccess, onFailure);
    }
    public static async Task MatchAsync(this Task<Result> resultTask, Func<Task> onSuccess, Func<Error, Task> onFailure)
    {
        var result = await resultTask;
        await result.MatchAsync(onSuccess, onFailure);
    }
    public static async Task MatchAsync<T1>(this Result<T1> result, Func<T1, Task> onSuccess, Action<Error> onFailure)
    {
        if (result.IsSuccess)
        {
            await onSuccess(result.Value);
            return;
        }

        onFailure(result.Error);
    }
    public static async Task MatchAsync<T1>(this Result<T1> result, Action<T1> onSuccess, Func<Error, Task> onFailure)
    {
        if (result.IsSuccess)
        {
            onSuccess(result.Value);
            return;
        }

        await onFailure(result.Error);
    }
    public static async Task MatchAsync<T1>(this Result<T1> result, Func<T1, Task> onSuccess, Func<Error, Task> onFailure)
    {
        if (result.IsSuccess)
        {
            await onSuccess(result.Value);
            return;
        }

        await onFailure(result.Error);
    }
    public static async Task MatchAsync<T1>(this Task<Result<T1>> resultTask, Action<T1> onSuccess, Action<Error> onFailure)
    {
        var result = await resultTask;
        result.Match(onSuccess, onFailure);
    }
    public static async Task MatchAsync<T1>(this Task<Result<T1>> resultTask, Func<T1, Task> onSuccess, Action<Error> onFailure)
    {
        var result = await resultTask;
        await result.MatchAsync(onSuccess, onFailure);
    }
    public static async Task MatchAsync<T1>(this Task<Result<T1>> resultTask, Action<T1> onSuccess, Func<Error, Task> onFailure)
    {
        var result = await resultTask;
        await result.MatchAsync(onSuccess, onFailure);
    }
    public static async Task MatchAsync<T1>(this Task<Result<T1>> resultTask, Func<T1, Task> onSuccess, Func<Error, Task> onFailure)
    {
        var result = await resultTask;
        await result.MatchAsync(onSuccess, onFailure);
    }
    public static async Task<TOut> MatchAsync<TOut>(this Result result, Func<Task<TOut>> onSuccess, Func<Error, TOut> onFailure)
        => result.IsSuccess ? await onSuccess() : onFailure(result.Error);
    public static async Task<TOut> MatchAsync<TOut>(this Result result, Func<TOut> onSuccess, Func<Error, Task<TOut>> onFailure)
        => result.IsSuccess ? onSuccess() : await onFailure(result.Error);
    public static async Task<TOut> MatchAsync<TOut>(this Result result, Func<Task<TOut>> onSuccess, Func<Error, Task<TOut>> onFailure)
        => result.IsSuccess ? await onSuccess() : await onFailure(result.Error);
    public static async Task<TOut> MatchAsync<TOut>(this Task<Result> resultTask, Func<TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        var result = await resultTask;
        return result.Match(onSuccess, onFailure);
    }
    public static async Task<TOut> MatchAsync<TOut>(this Task<Result> resultTask, Func<Task<TOut>> onSuccess, Func<Error, TOut> onFailure)
    {
        var result = await resultTask;
        return await result.MatchAsync(onSuccess, onFailure);
    }
    public static async Task<TOut> MatchAsync<TOut>(this Task<Result> resultTask, Func<TOut> onSuccess, Func<Error, Task<TOut>> onFailure)
    {
        var result = await resultTask;
        return await result.MatchAsync(onSuccess, onFailure);
    }
    public static async Task<TOut> MatchAsync<TOut>(this Task<Result> resultTask, Func<Task<TOut>> onSuccess, Func<Error, Task<TOut>> onFailure)
    {
        var result = await resultTask;
        return await result.MatchAsync(onSuccess, onFailure);
    }
    public static async Task<TOut> MatchAsync<TIn, TOut>(this Result<TIn> result, Func<TIn, Task<TOut>> onSuccess, Func<Error, TOut> onFailure)
        => result.IsSuccess ? await onSuccess(result.Value) : onFailure(result.Error);
    public static async Task<TOut> MatchAsync<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> onSuccess, Func<Error, Task<TOut>> onFailure)
        => result.IsSuccess ? onSuccess(result.Value) : await onFailure(result.Error);
    public static async Task<TOut> MatchAsync<TIn, TOut>(this Result<TIn> result, Func<TIn, Task<TOut>> onSuccess, Func<Error, Task<TOut>> onFailure)
        => result.IsSuccess ? await onSuccess(result.Value) : await onFailure(result.Error);
    public static async Task<TOut> MatchAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        var result = await resultTask;
        return result.Match(onSuccess, onFailure);
    }
    public static async Task<TOut> MatchAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, Task<TOut>> onSuccess, Func<Error, TOut> onFailure)
    {
        var result = await resultTask;
        return await result.MatchAsync(onSuccess, onFailure);
    }
    public static async Task<TOut> MatchAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, TOut> onSuccess, Func<Error, Task<TOut>> onFailure)
    {
        var result = await resultTask;
        return await result.MatchAsync(onSuccess, onFailure);
    }
    public static async Task<TOut> MatchAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, Task<TOut>> onSuccess, Func<Error, Task<TOut>> onFailure)
    {
        var result = await resultTask;
        return await result.MatchAsync(onSuccess, onFailure);
    }
}
