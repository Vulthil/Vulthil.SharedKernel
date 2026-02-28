namespace Vulthil.Results;

/// <summary>
/// Provides extension methods for <see cref="Result"/> and <see cref="Result{T}"/>.
/// </summary>
public static partial class ResultExtensions
{
    /// <summary>
    /// Invokes the success or failure action based on the result state.
    /// </summary>
    public static void Match(this Result result, Action onSuccess, Action<Error> onFailure)
    {
        if (result.IsSuccess)
        {
            onSuccess();
            return;
        }

        onFailure(result.Error);
    }
    /// <summary>
    /// Invokes the success or failure action based on the result state, passing the value on success.
    /// </summary>
    public static void Match<T1>(this Result<T1> result, Action<T1> onSuccess, Action<Error> onFailure)
    {
        if (result.IsSuccess)
        {
            onSuccess(result.Value);
            return;
        }

        onFailure(result.Error);
    }
    /// <summary>
    /// Returns a value produced by the success or failure function based on the result state.
    /// </summary>
    public static TOut Match<TOut>(this Result result, Func<TOut> onSuccess, Func<Error, TOut> onFailure)
        => result.IsSuccess ? onSuccess() : onFailure(result.Error);
    /// <summary>
    /// Returns a value produced by the success or failure function based on the result state, passing the value on success.
    /// </summary>
    public static TOut Match<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> onSuccess, Func<Error, TOut> onFailure)
        => result.IsSuccess ? onSuccess(result.Value) : onFailure(result.Error);

    /// <summary>
    /// Asynchronously invokes the success or failure action based on the result state.
    /// </summary>
    public static async Task MatchAsync(this Result result, Func<Task> onSuccess, Action<Error> onFailure)
    {
        if (result.IsSuccess)
        {
            await onSuccess();
            return;
        }

        onFailure(result.Error);
    }
    /// <inheritdoc cref="MatchAsync(Result, Func{Task}, Action{Error})"/>
    public static async Task MatchAsync(this Result result, Action onSuccess, Func<Error, Task> onFailure)
    {
        if (result.IsSuccess)
        {
            onSuccess();
            return;
        }

        await onFailure(result.Error);
    }
    /// <inheritdoc cref="MatchAsync(Result, Func{Task}, Action{Error})"/>
    public static async Task MatchAsync(this Result result, Func<Task> onSuccess, Func<Error, Task> onFailure)
    {
        if (result.IsSuccess)
        {
            await onSuccess();
            return;
        }

        await onFailure(result.Error);
    }
    /// <inheritdoc cref="MatchAsync(Result, Func{Task}, Action{Error})"/>
    public static async Task MatchAsync(this Task<Result> resultTask, Action onSuccess, Action<Error> onFailure) =>
        (await resultTask).Match(onSuccess, onFailure);
    /// <inheritdoc cref="MatchAsync(Result, Func{Task}, Action{Error})"/>
    public static async Task MatchAsync(this Task<Result> resultTask, Func<Task> onSuccess, Action<Error> onFailure) =>
        await (await resultTask).MatchAsync(onSuccess, onFailure);
    /// <inheritdoc cref="MatchAsync(Result, Func{Task}, Action{Error})"/>
    public static async Task MatchAsync(this Task<Result> resultTask, Action onSuccess, Func<Error, Task> onFailure) =>
        await (await resultTask).MatchAsync(onSuccess, onFailure);
    /// <inheritdoc cref="MatchAsync(Result, Func{Task}, Action{Error})"/>
    public static async Task MatchAsync(this Task<Result> resultTask, Func<Task> onSuccess, Func<Error, Task> onFailure) =>
        await (await resultTask).MatchAsync(onSuccess, onFailure);
    /// <summary>
    /// Asynchronously invokes the success or failure action based on the result state, passing the value on success.
    /// </summary>
    public static async Task MatchAsync<T1>(this Result<T1> result, Func<T1, Task> onSuccess, Action<Error> onFailure)
    {
        if (result.IsSuccess)
        {
            await onSuccess(result.Value);
            return;
        }

        onFailure(result.Error);
    }
    /// <inheritdoc cref="MatchAsync{T1}(Result{T1}, Func{T1, Task}, Action{Error})"/>
    public static async Task MatchAsync<T1>(this Result<T1> result, Action<T1> onSuccess, Func<Error, Task> onFailure)
    {
        if (result.IsSuccess)
        {
            onSuccess(result.Value);
            return;
        }

        await onFailure(result.Error);
    }
    /// <inheritdoc cref="MatchAsync{T1}(Result{T1}, Func{T1, Task}, Action{Error})"/>
    public static async Task MatchAsync<T1>(this Result<T1> result, Func<T1, Task> onSuccess, Func<Error, Task> onFailure)
    {
        if (result.IsSuccess)
        {
            await onSuccess(result.Value);
            return;
        }

        await onFailure(result.Error);
    }
    /// <inheritdoc cref="MatchAsync{T1}(Result{T1}, Func{T1, Task}, Action{Error})"/>
    public static async Task MatchAsync<T1>(this Task<Result<T1>> resultTask, Action<T1> onSuccess, Action<Error> onFailure) =>
        (await resultTask).Match(onSuccess, onFailure);
    /// <inheritdoc cref="MatchAsync{T1}(Result{T1}, Func{T1, Task}, Action{Error})"/>
    public static async Task MatchAsync<T1>(this Task<Result<T1>> resultTask, Func<T1, Task> onSuccess, Action<Error> onFailure) =>
        await (await resultTask).MatchAsync(onSuccess, onFailure);
    /// <inheritdoc cref="MatchAsync{T1}(Result{T1}, Func{T1, Task}, Action{Error})"/>
    public static async Task MatchAsync<T1>(this Task<Result<T1>> resultTask, Action<T1> onSuccess, Func<Error, Task> onFailure) =>
        await (await resultTask).MatchAsync(onSuccess, onFailure);
    /// <inheritdoc cref="MatchAsync{T1}(Result{T1}, Func{T1, Task}, Action{Error})"/>
    public static async Task MatchAsync<T1>(this Task<Result<T1>> resultTask, Func<T1, Task> onSuccess, Func<Error, Task> onFailure) =>
        await (await resultTask).MatchAsync(onSuccess, onFailure);
    /// <summary>
    /// Asynchronously returns a value produced by the success or failure function based on the result state.
    /// </summary>
    public static async Task<TOut> MatchAsync<TOut>(this Result result, Func<Task<TOut>> onSuccess, Func<Error, TOut> onFailure)
        => result.IsSuccess ? await onSuccess() : onFailure(result.Error);
    /// <inheritdoc cref="MatchAsync{TOut}(Result, Func{Task{TOut}}, Func{Error, TOut})"/>
    public static async Task<TOut> MatchAsync<TOut>(this Result result, Func<TOut> onSuccess, Func<Error, Task<TOut>> onFailure)
        => result.IsSuccess ? onSuccess() : await onFailure(result.Error);
    /// <inheritdoc cref="MatchAsync{TOut}(Result, Func{Task{TOut}}, Func{Error, TOut})"/>
    public static async Task<TOut> MatchAsync<TOut>(this Result result, Func<Task<TOut>> onSuccess, Func<Error, Task<TOut>> onFailure)
        => result.IsSuccess ? await onSuccess() : await onFailure(result.Error);
    /// <inheritdoc cref="MatchAsync{TOut}(Result, Func{Task{TOut}}, Func{Error, TOut})"/>
    public static async Task<TOut> MatchAsync<TOut>(this Task<Result> resultTask, Func<TOut> onSuccess, Func<Error, TOut> onFailure) =>
        (await resultTask).Match(onSuccess, onFailure);
    /// <inheritdoc cref="MatchAsync{TOut}(Result, Func{Task{TOut}}, Func{Error, TOut})"/>
    public static async Task<TOut> MatchAsync<TOut>(this Task<Result> resultTask, Func<Task<TOut>> onSuccess, Func<Error, TOut> onFailure) =>
        await (await resultTask).MatchAsync(onSuccess, onFailure);
    /// <inheritdoc cref="MatchAsync{TOut}(Result, Func{Task{TOut}}, Func{Error, TOut})"/>
    public static async Task<TOut> MatchAsync<TOut>(this Task<Result> resultTask, Func<TOut> onSuccess, Func<Error, Task<TOut>> onFailure) =>
        await (await resultTask).MatchAsync(onSuccess, onFailure);
    /// <inheritdoc cref="MatchAsync{TOut}(Result, Func{Task{TOut}}, Func{Error, TOut})"/>
    public static async Task<TOut> MatchAsync<TOut>(this Task<Result> resultTask, Func<Task<TOut>> onSuccess, Func<Error, Task<TOut>> onFailure) =>
        await (await resultTask).MatchAsync(onSuccess, onFailure);
    /// <summary>
    /// Asynchronously returns a value produced by the success or failure function based on the result state, passing the value on success.
    /// </summary>
    public static async Task<TOut> MatchAsync<TIn, TOut>(this Result<TIn> result, Func<TIn, Task<TOut>> onSuccess, Func<Error, TOut> onFailure)
        => result.IsSuccess ? await onSuccess(result.Value) : onFailure(result.Error);
    /// <inheritdoc cref="MatchAsync{TIn, TOut}(Result{TIn}, Func{TIn, Task{TOut}}, Func{Error, TOut})"/>
    public static async Task<TOut> MatchAsync<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> onSuccess, Func<Error, Task<TOut>> onFailure)
        => result.IsSuccess ? onSuccess(result.Value) : await onFailure(result.Error);
    /// <inheritdoc cref="MatchAsync{TIn, TOut}(Result{TIn}, Func{TIn, Task{TOut}}, Func{Error, TOut})"/>
    public static async Task<TOut> MatchAsync<TIn, TOut>(this Result<TIn> result, Func<TIn, Task<TOut>> onSuccess, Func<Error, Task<TOut>> onFailure)
        => result.IsSuccess ? await onSuccess(result.Value) : await onFailure(result.Error);
    /// <inheritdoc cref="MatchAsync{TIn, TOut}(Result{TIn}, Func{TIn, Task{TOut}}, Func{Error, TOut})"/>
    public static async Task<TOut> MatchAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        (await resultTask).Match(onSuccess, onFailure);
    /// <inheritdoc cref="MatchAsync{TIn, TOut}(Result{TIn}, Func{TIn, Task{TOut}}, Func{Error, TOut})"/>
    public static async Task<TOut> MatchAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, Task<TOut>> onSuccess, Func<Error, TOut> onFailure) =>
        await (await resultTask).MatchAsync(onSuccess, onFailure);
    /// <inheritdoc cref="MatchAsync{TIn, TOut}(Result{TIn}, Func{TIn, Task{TOut}}, Func{Error, TOut})"/>
    public static async Task<TOut> MatchAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, TOut> onSuccess, Func<Error, Task<TOut>> onFailure) =>
        await (await resultTask).MatchAsync(onSuccess, onFailure);
    /// <inheritdoc cref="MatchAsync{TIn, TOut}(Result{TIn}, Func{TIn, Task{TOut}}, Func{Error, TOut})"/>
    public static async Task<TOut> MatchAsync<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, Task<TOut>> onSuccess, Func<Error, Task<TOut>> onFailure) =>
        await (await resultTask).MatchAsync(onSuccess, onFailure);
}
