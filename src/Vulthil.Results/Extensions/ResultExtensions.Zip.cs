namespace Vulthil.Results;

/// <summary>
/// Provides extension methods for <see cref="Result"/> and <see cref="Result{T}"/>.
/// </summary>
public static partial class ResultExtensions
{
    /// <summary>
    /// Combines two successful results into a tuple; when one fails its error propagates, and when both fail their errors are aggregated into a <see cref="ValidationError"/>.
    /// </summary>
    public static Result<(T1, T2)> Zip<T1, T2>(this Result<T1> first, Result<T2> second) =>
        first.IsSuccess && second.IsSuccess
            ? Result.Success((first.Value, second.Value))
            : Result.Failure<(T1, T2)>(CombineErrors(first, second));
    /// <summary>
    /// Combines two successful results using the selector; when one fails its error propagates, and when both fail their errors are aggregated into a <see cref="ValidationError"/>.
    /// </summary>
    public static Result<TOut> Zip<T1, T2, TOut>(this Result<T1> first, Result<T2> second, Func<T1, T2, TOut> selector) =>
        first.IsSuccess && second.IsSuccess
            ? Result.Success(selector(first.Value, second.Value))
            : Result.Failure<TOut>(CombineErrors(first, second));

    /// <inheritdoc cref="Zip{T1, T2}(Result{T1}, Result{T2})"/>
    public static async Task<Result<(T1, T2)>> ZipAsync<T1, T2>(this Task<Result<T1>> firstTask, Result<T2> second) =>
        (await firstTask).Zip(second);
    /// <inheritdoc cref="Zip{T1, T2}(Result{T1}, Result{T2})"/>
    public static async Task<Result<(T1, T2)>> ZipAsync<T1, T2>(this Task<Result<T1>> firstTask, Task<Result<T2>> secondTask) =>
        (await firstTask).Zip(await secondTask);
    /// <inheritdoc cref="Zip{T1, T2, TOut}(Result{T1}, Result{T2}, Func{T1, T2, TOut})"/>
    public static async Task<Result<TOut>> ZipAsync<T1, T2, TOut>(this Task<Result<T1>> firstTask, Result<T2> second, Func<T1, T2, TOut> selector) =>
        (await firstTask).Zip(second, selector);
    /// <inheritdoc cref="Zip{T1, T2, TOut}(Result{T1}, Result{T2}, Func{T1, T2, TOut})"/>
    public static async Task<Result<TOut>> ZipAsync<T1, T2, TOut>(this Task<Result<T1>> firstTask, Task<Result<T2>> secondTask, Func<T1, T2, TOut> selector) =>
        (await firstTask).Zip(await secondTask, selector);

    private static Error CombineErrors(Result first, Result second)
    {
        if (first.IsFailure && second.IsFailure)
        {
            return new ValidationError([first.Error, second.Error]);
        }

        return first.IsFailure ? first.Error : second.Error;
    }
}
