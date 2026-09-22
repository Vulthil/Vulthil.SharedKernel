using Vulthil.xUnit;

namespace Vulthil.Results.Tests.Extensions;

public sealed class TapErrorAsyncTests : BaseUnitTestCase
{
    private static readonly Error FirstError = Error.Failure("First", "first failed");

    [Fact]
    public async Task TapErrorAsyncAwaitsAsyncActionWithErrorOnFailure()
    {
        // Arrange
        var result = Result.Failure(FirstError);
        var actionGate = new TaskCompletionSource();
        Error? captured = null;

        // Act
        var pending = result.TapErrorAsync(error =>
        {
            captured = error;
            return actionGate.Task;
        });
        var completedBeforeAction = pending.IsCompleted;
        actionGate.SetResult();
        var tapped = await pending;

        // Assert
        completedBeforeAction.ShouldBeFalse();
        captured.ShouldBe(FirstError);
        tapped.ShouldBeSameAs(result);
    }

    [Fact]
    public async Task TapErrorAsyncSkipsAsyncActionOnSuccess()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var tapped = await result.TapErrorAsync(ActionShouldNotRunAsync);

        // Assert
        tapped.ShouldBeSameAs(result);
    }

    [Fact]
    public async Task TapErrorAsyncOnTaskSourceInvokesActionWithErrorOnFailure()
    {
        // Arrange
        var result = Result.Failure(FirstError);
        var resultTask = Task.FromResult(result);
        Error? captured = null;

        // Act
        var tapped = await resultTask.TapErrorAsync(error => captured = error);

        // Assert
        captured.ShouldBe(FirstError);
        tapped.ShouldBeSameAs(result);
    }

    [Fact]
    public async Task TapErrorAsyncOnTaskSourceSkipsActionOnSuccess()
    {
        // Arrange
        var result = Result.Success();
        var resultTask = Task.FromResult(result);

        // Act
        var tapped = await resultTask.TapErrorAsync(ActionShouldNotRun);

        // Assert
        tapped.ShouldBeSameAs(result);
    }

    [Fact]
    public async Task TapErrorAsyncOnTaskSourceAwaitsAsyncActionWithErrorOnFailure()
    {
        // Arrange
        var result = Result.Failure(FirstError);
        var resultTask = Task.FromResult(result);
        var actionGate = new TaskCompletionSource();
        Error? captured = null;

        // Act
        var pending = resultTask.TapErrorAsync(error =>
        {
            captured = error;
            return actionGate.Task;
        });
        var completedBeforeAction = pending.IsCompleted;
        actionGate.SetResult();
        var tapped = await pending;

        // Assert
        completedBeforeAction.ShouldBeFalse();
        captured.ShouldBe(FirstError);
        tapped.ShouldBeSameAs(result);
    }

    [Fact]
    public async Task TapErrorAsyncOnTaskSourceSkipsAsyncActionOnSuccess()
    {
        // Arrange
        var result = Result.Success();
        var resultTask = Task.FromResult(result);

        // Act
        var tapped = await resultTask.TapErrorAsync(ActionShouldNotRunAsync);

        // Assert
        tapped.ShouldBeSameAs(result);
    }

    [Fact]
    public async Task TapErrorAsyncAwaitsAsyncActionWithErrorOnFailureForTypedResult()
    {
        // Arrange
        var result = Result.Failure<int>(FirstError);
        var actionGate = new TaskCompletionSource();
        Error? captured = null;

        // Act
        var pending = result.TapErrorAsync(error =>
        {
            captured = error;
            return actionGate.Task;
        });
        var completedBeforeAction = pending.IsCompleted;
        actionGate.SetResult();
        var tapped = await pending;

        // Assert
        completedBeforeAction.ShouldBeFalse();
        captured.ShouldBe(FirstError);
        tapped.ShouldBeSameAs(result);
    }

    [Fact]
    public async Task TapErrorAsyncSkipsAsyncActionOnSuccessForTypedResult()
    {
        // Arrange
        var result = Result.Success(7);

        // Act
        var tapped = await result.TapErrorAsync(ActionShouldNotRunAsync);

        // Assert
        tapped.ShouldBeSameAs(result);
        tapped.Value.ShouldBe(7);
    }

    [Fact]
    public async Task TapErrorAsyncOnTaskSourceInvokesActionWithErrorOnFailureForTypedResult()
    {
        // Arrange
        var result = Result.Failure<int>(FirstError);
        var resultTask = Task.FromResult(result);
        Error? captured = null;

        // Act
        var tapped = await resultTask.TapErrorAsync(error => captured = error);

        // Assert
        captured.ShouldBe(FirstError);
        tapped.ShouldBeSameAs(result);
    }

    [Fact]
    public async Task TapErrorAsyncOnTaskSourceSkipsActionOnSuccessForTypedResult()
    {
        // Arrange
        var result = Result.Success(7);
        var resultTask = Task.FromResult(result);

        // Act
        var tapped = await resultTask.TapErrorAsync(ActionShouldNotRun);

        // Assert
        tapped.ShouldBeSameAs(result);
        tapped.Value.ShouldBe(7);
    }

    [Fact]
    public async Task TapErrorAsyncOnTaskSourceAwaitsAsyncActionWithErrorOnFailureForTypedResult()
    {
        // Arrange
        var result = Result.Failure<int>(FirstError);
        var resultTask = Task.FromResult(result);
        var actionGate = new TaskCompletionSource();
        Error? captured = null;

        // Act
        var pending = resultTask.TapErrorAsync(error =>
        {
            captured = error;
            return actionGate.Task;
        });
        var completedBeforeAction = pending.IsCompleted;
        actionGate.SetResult();
        var tapped = await pending;

        // Assert
        completedBeforeAction.ShouldBeFalse();
        captured.ShouldBe(FirstError);
        tapped.ShouldBeSameAs(result);
    }

    [Fact]
    public async Task TapErrorAsyncOnTaskSourceSkipsAsyncActionOnSuccessForTypedResult()
    {
        // Arrange
        var result = Result.Success(7);
        var resultTask = Task.FromResult(result);

        // Act
        var tapped = await resultTask.TapErrorAsync(ActionShouldNotRunAsync);

        // Assert
        tapped.ShouldBeSameAs(result);
        tapped.Value.ShouldBe(7);
    }

    private static void ActionShouldNotRun(Error _) =>
        throw new InvalidOperationException("should not run");

    private static Task ActionShouldNotRunAsync(Error _) =>
        throw new InvalidOperationException("should not run");
}
