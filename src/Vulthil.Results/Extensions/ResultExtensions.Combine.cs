namespace Vulthil.Results;

/// <summary>
/// Provides extension methods for <see cref="Result"/> and <see cref="Result{T}"/>.
/// </summary>
public static partial class ResultExtensions
{
    /// <summary>
    /// Combines multiple results, returning success when all succeed; a single failure's original error when exactly
    /// one fails; otherwise a failure with a <see cref="ValidationError"/> aggregating every failed result's error.
    /// </summary>
    public static Result Combine(params Result[] results) =>
        Combine((IEnumerable<Result>)results);
    /// <summary>
    /// Combines a sequence of results, returning success when all succeed; a single failure's original error when
    /// exactly one fails; otherwise a failure with a <see cref="ValidationError"/> aggregating every failed result's error.
    /// </summary>
    public static Result Combine(this IEnumerable<Result> results)
    {
        Error[] failedErrors = [.. results.Where(result => result.IsFailure).Select(result => result.Error)];
        return failedErrors.Length switch
        {
            0 => Result.Success(),
            1 => Result.Failure(failedErrors[0]),
            _ => Result.Failure(new ValidationError(failedErrors))
        };
    }
    /// <summary>
    /// Combines multiple typed results into one result carrying every success value in source order, returning
    /// success when all succeed; a single failure's original error when exactly one fails; otherwise a failure with a
    /// <see cref="ValidationError"/> aggregating every failed result's error.
    /// </summary>
    public static Result<IReadOnlyList<T>> Combine<T>(params Result<T>[] results) =>
        Combine((IEnumerable<Result<T>>)results);
    /// <summary>
    /// Combines a sequence of typed results into one result carrying every success value in source order, returning
    /// success when all succeed; a single failure's original error when exactly one fails; otherwise a failure with a
    /// <see cref="ValidationError"/> aggregating every failed result's error.
    /// </summary>
    public static Result<IReadOnlyList<T>> Combine<T>(this IEnumerable<Result<T>> results)
    {
        Result<T>[] items = [.. results];
        Error[] failedErrors = [.. items.Where(result => result.IsFailure).Select(result => result.Error)];
        if (failedErrors.Length > 0)
        {
            return Result.Failure<IReadOnlyList<T>>(failedErrors.Length == 1 ? failedErrors[0] : new ValidationError(failedErrors));
        }

        T[] values = [.. items.Select(result => result.Value)];
        return Result.Success<IReadOnlyList<T>>(values);
    }

    /// <summary>
    /// Asynchronously awaits and combines a sequence of result tasks, returning success when all succeed; a single
    /// failure's original error when exactly one fails; otherwise a failure with a <see cref="ValidationError"/>
    /// aggregating every failed result's error.
    /// </summary>
    public static async Task<Result> CombineAsync(this IEnumerable<Task<Result>> resultTasks) =>
        (await Task.WhenAll(resultTasks).ConfigureAwait(false)).Combine();
    /// <inheritdoc cref="CombineAsync(IEnumerable{Task{Result}})"/>
    public static Task<Result> CombineAsync(params Task<Result>[] resultTasks) =>
        CombineAsync((IEnumerable<Task<Result>>)resultTasks);
    /// <summary>
    /// Asynchronously awaits and combines a sequence of typed result tasks into one result carrying every success
    /// value in source order, returning success when all succeed; a single failure's original error when exactly one
    /// fails; otherwise a failure with a <see cref="ValidationError"/> aggregating every failed result's error.
    /// </summary>
    public static async Task<Result<IReadOnlyList<T>>> CombineAsync<T>(this IEnumerable<Task<Result<T>>> resultTasks) =>
        (await Task.WhenAll(resultTasks).ConfigureAwait(false)).Combine();
    /// <inheritdoc cref="CombineAsync{T}(IEnumerable{Task{Result{T}}})"/>
    public static Task<Result<IReadOnlyList<T>>> CombineAsync<T>(params Task<Result<T>>[] resultTasks) =>
        CombineAsync((IEnumerable<Task<Result<T>>>)resultTasks);
}
