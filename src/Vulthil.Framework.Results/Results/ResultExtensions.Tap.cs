using Vulthil.Framework.Results.Results;

namespace Vulthil.SharedKernel.Primitives;

public static partial class ResultExtensions
{
    public static Result Tap(this Result result, Action action)
    {
        if (result.IsSuccess)
        {
            action();
        }

        return result;
    }
    public static Result<T1> Tap<T1>(this Result<T1> result, Action action)
    {
        if (result.IsSuccess)
        {
            action();
        }

        return result;
    }
    public static Result<T1> Tap<T1>(this Result<T1> result, Action<T1> action)
    {
        if (result.IsSuccess)
        {
            action(result.Value);
        }

        return result;
    }

    public static async Task<Result> TapAsync(this Result result, Func<Task> action)
    {
        if (result.IsSuccess)
        {
            await action();
        }

        return result;
    }
    public static async Task<Result> TapAsync(this Task<Result> resultTask, Action action) =>
        (await resultTask).Tap(action);
    public static async Task<Result> TapAsync(this Task<Result> resultTask, Func<Task> action) =>
        await (await resultTask).TapAsync(action);
    public static async Task<Result<T1>> TapAsync<T1>(this Result<T1> result, Func<Task> action)
    {
        if (result.IsSuccess)
        {
            await action();
        }

        return result;
    }
    public static async Task<Result<T1>> TapAsync<T1>(this Result<T1> result, Func<T1, Task> action)
    {
        if (result.IsSuccess)
        {
            await action(result.Value);
        }

        return result;
    }

    public static async Task<Result<T1>> TapAsync<T1>(this Task<Result<T1>> resultTask, Action action) =>
         (await resultTask).Tap(action);
    public static async Task<Result<T1>> TapAsync<T1>(this Task<Result<T1>> resultTask, Func<Task> action) =>
        await (await resultTask).TapAsync(action);
    public static async Task<Result<T1>> TapAsync<T1>(this Task<Result<T1>> resultTask, Action<T1> action) =>
        (await resultTask).Tap(action);
    public static async Task<Result<T1>> TapAsync<T1>(this Task<Result<T1>> resultTask, Func<T1, Task> action) =>
        await (await resultTask).TapAsync(action);
}
