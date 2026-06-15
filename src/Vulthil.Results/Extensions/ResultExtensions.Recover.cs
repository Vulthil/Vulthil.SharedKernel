namespace Vulthil.Results;

/// <summary>
/// Provides extension methods for <see cref="Result"/> and <see cref="Result{T}"/>.
/// </summary>
public static partial class ResultExtensions
{
    /// <summary>
    /// Recovers from a failure by producing a success value from the error; successful results propagate unchanged.
    /// </summary>
    public static Result<T1> Recover<T1>(this Result<T1> result, Func<Error, T1> recover) =>
        result.IsSuccess ? result : Result.Success(recover(result.Error));

    /// <summary>
    /// Asynchronously recovers from a failure by producing a success value from the error; successful results propagate unchanged.
    /// </summary>
    public static async Task<Result<T1>> RecoverAsync<T1>(this Result<T1> result, Func<Error, Task<T1>> recover) =>
        result.IsSuccess ? result : Result.Success(await recover(result.Error));
    /// <inheritdoc cref="Recover{T1}(Result{T1}, Func{Error, T1})"/>
    public static async Task<Result<T1>> RecoverAsync<T1>(this Task<Result<T1>> resultTask, Func<Error, T1> recover) =>
        (await resultTask).Recover(recover);
    /// <inheritdoc cref="RecoverAsync{T1}(Result{T1}, Func{Error, Task{T1}})"/>
    public static async Task<Result<T1>> RecoverAsync<T1>(this Task<Result<T1>> resultTask, Func<Error, Task<T1>> recover) =>
        await (await resultTask).RecoverAsync(recover);
}
