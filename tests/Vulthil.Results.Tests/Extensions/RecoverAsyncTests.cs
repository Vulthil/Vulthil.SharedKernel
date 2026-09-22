using Vulthil.xUnit;

namespace Vulthil.Results.Tests.Extensions;

public sealed class RecoverAsyncTests : BaseUnitTestCase
{
    private static readonly Error FirstError = Error.Failure("First", "first failed");

    [Fact]
    public async Task RecoverAsyncInvokesAsyncRecoveryWithErrorOnFailure()
    {
        // Arrange
        var result = Result.Failure<int>(FirstError);
        Error? captured = null;

        // Act
        var recovered = await result.RecoverAsync(error =>
        {
            captured = error;
            return Task.FromResult(42);
        });

        // Assert
        captured.ShouldBe(FirstError);
        recovered.IsSuccess.ShouldBeTrue();
        recovered.Value.ShouldBe(42);
    }

    [Fact]
    public async Task RecoverAsyncSkipsAsyncRecoveryOnSuccess()
    {
        // Arrange
        var result = Result.Success(7);

        // Act
        var recovered = await result.RecoverAsync(RecoveryShouldNotRunAsync);

        // Assert
        recovered.ShouldBeSameAs(result);
        recovered.Value.ShouldBe(7);
    }

    [Fact]
    public async Task RecoverAsyncOnTaskSourceInvokesRecoveryWithErrorOnFailure()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure<int>(FirstError));
        Error? captured = null;

        // Act
        var recovered = await resultTask.RecoverAsync(error =>
        {
            captured = error;
            return 42;
        });

        // Assert
        captured.ShouldBe(FirstError);
        recovered.IsSuccess.ShouldBeTrue();
        recovered.Value.ShouldBe(42);
    }

    [Fact]
    public async Task RecoverAsyncOnTaskSourceSkipsRecoveryOnSuccess()
    {
        // Arrange
        var result = Result.Success(7);
        var resultTask = Task.FromResult(result);

        // Act
        var recovered = await resultTask.RecoverAsync(RecoveryShouldNotRun);

        // Assert
        recovered.ShouldBeSameAs(result);
        recovered.Value.ShouldBe(7);
    }

    [Fact]
    public async Task RecoverAsyncOnTaskSourceInvokesAsyncRecoveryWithErrorOnFailure()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure<int>(FirstError));
        Error? captured = null;

        // Act
        var recovered = await resultTask.RecoverAsync(error =>
        {
            captured = error;
            return Task.FromResult(42);
        });

        // Assert
        captured.ShouldBe(FirstError);
        recovered.IsSuccess.ShouldBeTrue();
        recovered.Value.ShouldBe(42);
    }

    [Fact]
    public async Task RecoverAsyncOnTaskSourceSkipsAsyncRecoveryOnSuccess()
    {
        // Arrange
        var result = Result.Success(7);
        var resultTask = Task.FromResult(result);

        // Act
        var recovered = await resultTask.RecoverAsync(RecoveryShouldNotRunAsync);

        // Assert
        recovered.ShouldBeSameAs(result);
        recovered.Value.ShouldBe(7);
    }

    private static int RecoveryShouldNotRun(Error _) =>
        throw new InvalidOperationException("should not run");

    private static Task<int> RecoveryShouldNotRunAsync(Error _) =>
        throw new InvalidOperationException("should not run");
}
