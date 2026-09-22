using Vulthil.xUnit;

namespace Vulthil.Results.Tests.Extensions;

public sealed class OrElseAsyncTests : BaseUnitTestCase
{
    private static readonly Error FirstError = Error.Failure("First", "first failed");

    [Fact]
    public async Task OrAsyncReturnsFallbackOnFailure()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure(FirstError));
        var fallback = Result.Success();

        // Act
        var combined = await resultTask.OrAsync(fallback);

        // Assert
        combined.ShouldBeSameAs(fallback);
    }

    [Fact]
    public async Task OrAsyncReturnsOriginalOnSuccess()
    {
        // Arrange
        var result = Result.Success();
        var resultTask = Task.FromResult(result);

        // Act
        var combined = await resultTask.OrAsync(Result.Failure(FirstError));

        // Assert
        combined.ShouldBeSameAs(result);
    }

    [Fact]
    public async Task OrAsyncReturnsFallbackOnFailureForTypedResult()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure<int>(FirstError));
        var fallback = Result.Success(99);

        // Act
        var combined = await resultTask.OrAsync(fallback);

        // Assert
        combined.ShouldBeSameAs(fallback);
        combined.Value.ShouldBe(99);
    }

    [Fact]
    public async Task OrAsyncReturnsOriginalOnSuccessForTypedResult()
    {
        // Arrange
        var result = Result.Success(1);
        var resultTask = Task.FromResult(result);

        // Act
        var combined = await resultTask.OrAsync(Result.Success(99));

        // Assert
        combined.ShouldBeSameAs(result);
        combined.Value.ShouldBe(1);
    }

    [Fact]
    public async Task OrElseAsyncInvokesAsyncFallbackWithErrorOnFailure()
    {
        // Arrange
        var result = Result.Failure(FirstError);
        var fallback = Result.Success();
        Error? captured = null;

        // Act
        var combined = await result.OrElseAsync(error =>
        {
            captured = error;
            return Task.FromResult(fallback);
        });

        // Assert
        captured.ShouldBe(FirstError);
        combined.ShouldBeSameAs(fallback);
    }

    [Fact]
    public async Task OrElseAsyncSkipsAsyncFallbackOnSuccess()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var combined = await result.OrElseAsync(FallbackShouldNotRunAsync);

        // Assert
        combined.ShouldBeSameAs(result);
    }

    [Fact]
    public async Task OrElseAsyncOnTaskSourceInvokesFallbackWithErrorOnFailure()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure(FirstError));
        var fallback = Result.Success();
        Error? captured = null;

        // Act
        var combined = await resultTask.OrElseAsync(error =>
        {
            captured = error;
            return fallback;
        });

        // Assert
        captured.ShouldBe(FirstError);
        combined.ShouldBeSameAs(fallback);
    }

    [Fact]
    public async Task OrElseAsyncOnTaskSourceSkipsFallbackOnSuccess()
    {
        // Arrange
        var result = Result.Success();
        var resultTask = Task.FromResult(result);

        // Act
        var combined = await resultTask.OrElseAsync(FallbackShouldNotRun);

        // Assert
        combined.ShouldBeSameAs(result);
    }

    [Fact]
    public async Task OrElseAsyncOnTaskSourceInvokesAsyncFallbackWithErrorOnFailure()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure(FirstError));
        var fallback = Result.Success();
        Error? captured = null;

        // Act
        var combined = await resultTask.OrElseAsync(error =>
        {
            captured = error;
            return Task.FromResult(fallback);
        });

        // Assert
        captured.ShouldBe(FirstError);
        combined.ShouldBeSameAs(fallback);
    }

    [Fact]
    public async Task OrElseAsyncOnTaskSourceSkipsAsyncFallbackOnSuccess()
    {
        // Arrange
        var result = Result.Success();
        var resultTask = Task.FromResult(result);

        // Act
        var combined = await resultTask.OrElseAsync(FallbackShouldNotRunAsync);

        // Assert
        combined.ShouldBeSameAs(result);
    }

    [Fact]
    public async Task OrElseAsyncInvokesAsyncFallbackWithErrorOnFailureForTypedResult()
    {
        // Arrange
        var result = Result.Failure<int>(FirstError);
        var fallback = Result.Success(123);
        Error? captured = null;

        // Act
        var combined = await result.OrElseAsync(error =>
        {
            captured = error;
            return Task.FromResult(fallback);
        });

        // Assert
        captured.ShouldBe(FirstError);
        combined.ShouldBeSameAs(fallback);
        combined.Value.ShouldBe(123);
    }

    [Fact]
    public async Task OrElseAsyncSkipsAsyncFallbackOnSuccessForTypedResult()
    {
        // Arrange
        var result = Result.Success(5);

        // Act
        var combined = await result.OrElseAsync(TypedFallbackShouldNotRunAsync);

        // Assert
        combined.ShouldBeSameAs(result);
        combined.Value.ShouldBe(5);
    }

    [Fact]
    public async Task OrElseAsyncOnTaskSourceInvokesFallbackWithErrorOnFailureForTypedResult()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure<int>(FirstError));
        var fallback = Result.Success(123);
        Error? captured = null;

        // Act
        var combined = await resultTask.OrElseAsync(error =>
        {
            captured = error;
            return fallback;
        });

        // Assert
        captured.ShouldBe(FirstError);
        combined.ShouldBeSameAs(fallback);
        combined.Value.ShouldBe(123);
    }

    [Fact]
    public async Task OrElseAsyncOnTaskSourceSkipsFallbackOnSuccessForTypedResult()
    {
        // Arrange
        var result = Result.Success(5);
        var resultTask = Task.FromResult(result);

        // Act
        var combined = await resultTask.OrElseAsync(TypedFallbackShouldNotRun);

        // Assert
        combined.ShouldBeSameAs(result);
        combined.Value.ShouldBe(5);
    }

    [Fact]
    public async Task OrElseAsyncOnTaskSourceInvokesAsyncFallbackWithErrorOnFailureForTypedResult()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure<int>(FirstError));
        var fallback = Result.Success(123);
        Error? captured = null;

        // Act
        var combined = await resultTask.OrElseAsync(error =>
        {
            captured = error;
            return Task.FromResult(fallback);
        });

        // Assert
        captured.ShouldBe(FirstError);
        combined.ShouldBeSameAs(fallback);
        combined.Value.ShouldBe(123);
    }

    [Fact]
    public async Task OrElseAsyncOnTaskSourceSkipsAsyncFallbackOnSuccessForTypedResult()
    {
        // Arrange
        var result = Result.Success(5);
        var resultTask = Task.FromResult(result);

        // Act
        var combined = await resultTask.OrElseAsync(TypedFallbackShouldNotRunAsync);

        // Assert
        combined.ShouldBeSameAs(result);
        combined.Value.ShouldBe(5);
    }

    private static Result FallbackShouldNotRun(Error _) =>
        throw new InvalidOperationException("should not run");

    private static Task<Result> FallbackShouldNotRunAsync(Error _) =>
        throw new InvalidOperationException("should not run");

    private static Result<int> TypedFallbackShouldNotRun(Error _) =>
        throw new InvalidOperationException("should not run");

    private static Task<Result<int>> TypedFallbackShouldNotRunAsync(Error _) =>
        throw new InvalidOperationException("should not run");
}
