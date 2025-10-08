namespace Vulthil.Results;

public static partial class ResultExtensions
{
    public static Result<T> ToResult<T>(in this T? nullable, Error error)
        where T : struct
    {
        if (!nullable.HasValue)
        {
            return Result.Failure<T>(error);
        }

        return Result.Success(nullable.Value);
    }
    public static Result<T> ToResult<T>(this T? obj, Error error)
        where T : class
    {
        if (obj is null)
        {
            return Result.Failure<T>(error);
        }

        return Result.Success(obj);
    }

    public static async Task<Result<T>> ToResultAsync<T>(this Task<T?> nullableTask, Error errors)
        where T : struct
    {
        var nullable = await nullableTask.ConfigureAwait(false);
        return nullable.ToResult(errors);
    }

    public static async Task<Result<T>> ToResultAsync<T>(this Task<T?> nullableTask, Error errors)
        where T : class
    {
        var nullable = await nullableTask.ConfigureAwait(false);
        return nullable.ToResult(errors);
    }
}
