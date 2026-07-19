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
    /// Asynchronously awaits and combines a sequence of result tasks, returning success when all succeed; a single
    /// failure's original error when exactly one fails; otherwise a failure with a <see cref="ValidationError"/>
    /// aggregating every failed result's error.
    /// </summary>
    public static async Task<Result> CombineAsync(this IEnumerable<Task<Result>> resultTasks) =>
        (await Task.WhenAll(resultTasks)).Combine();
}
