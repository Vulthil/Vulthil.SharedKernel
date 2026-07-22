namespace Vulthil.Results;

/// <summary>
/// Provides extension methods for <see cref="Result"/> and <see cref="Result{T}"/>.
/// </summary>
public static partial class ResultExtensions
{
    /// <summary>
    /// Returns the success value, or <see langword="default"/> when the result is a failure.
    /// </summary>
    public static T1? GetValueOrDefault<T1>(this Result<T1> result) =>
        result.IsSuccess ? result.Value : default;
    /// <summary>
    /// Returns the success value, or the specified fallback when the result is a failure.
    /// </summary>
    public static T1 GetValueOrDefault<T1>(this Result<T1> result, T1 fallback) =>
        result.IsSuccess ? result.Value : fallback;

    /// <inheritdoc cref="GetValueOrDefault{T1}(Result{T1})"/>
    public static async Task<T1?> GetValueOrDefaultAsync<T1>(this Task<Result<T1>> resultTask) =>
        (await resultTask.ConfigureAwait(false)).GetValueOrDefault();
    /// <inheritdoc cref="GetValueOrDefault{T1}(Result{T1}, T1)"/>
    public static async Task<T1> GetValueOrDefaultAsync<T1>(this Task<Result<T1>> resultTask, T1 fallback) =>
        (await resultTask.ConfigureAwait(false)).GetValueOrDefault(fallback);
}
