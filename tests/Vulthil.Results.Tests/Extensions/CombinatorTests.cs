using Vulthil.xUnit;

namespace Vulthil.Results.Tests.Extensions;

public sealed class CombinatorTests : BaseUnitTestCase
{
    private static readonly Error FirstError = Error.Failure("First", "first failed");
    private static readonly Error SecondError = Error.Failure("Second", "second failed");
    private static readonly Error EnsureError = Error.Validation("Ensure", "predicate failed");

    [Fact]
    public void EnsureReturnsResultWhenPredicatePasses()
    {
        // Arrange
        var result = Result.Success(10);

        // Act
        var ensured = result.Ensure(value => value > 5, EnsureError);

        // Assert
        ensured.IsSuccess.ShouldBeTrue();
        ensured.Value.ShouldBe(10);
    }

    [Fact]
    public void EnsureReturnsFailureWhenPredicateFails()
    {
        // Arrange
        var result = Result.Success(1);

        // Act
        var ensured = result.Ensure(value => value > 5, EnsureError);

        // Assert
        ensured.IsFailure.ShouldBeTrue();
        ensured.Error.ShouldBe(EnsureError);
    }

    [Fact]
    public void EnsureLeavesFailureUnchangedWithoutEvaluatingPredicate()
    {
        // Arrange
        var result = Result.Failure<int>(FirstError);

        // Act
        var ensured = result.Ensure(_ => throw new InvalidOperationException("should not run"), EnsureError);

        // Assert
        ensured.IsFailure.ShouldBeTrue();
        ensured.Error.ShouldBe(FirstError);
    }

    [Fact]
    public async Task EnsureAsyncAppliesPredicateOnTaskSource()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Success(1));

        // Act
        var ensured = await resultTask.EnsureAsync(value => Task.FromResult(value > 5), EnsureError);

        // Assert
        ensured.IsFailure.ShouldBeTrue();
        ensured.Error.ShouldBe(EnsureError);
    }

    [Fact]
    public void GetValueOrDefaultReturnsValueOnSuccess()
    {
        // Arrange
        var result = Result.Success("value");

        // Act
        var value = result.GetValueOrDefault();

        // Assert
        value.ShouldBe("value");
    }

    [Fact]
    public void GetValueOrDefaultReturnsDefaultOnFailure()
    {
        // Arrange
        var result = Result.Failure<string>(FirstError);

        // Act
        var value = result.GetValueOrDefault();

        // Assert
        value.ShouldBeNull();
    }

    [Fact]
    public void GetValueOrDefaultReturnsFallbackOnFailure()
    {
        // Arrange
        var result = Result.Failure<string>(FirstError);

        // Act
        var value = result.GetValueOrDefault("fallback");

        // Assert
        value.ShouldBe("fallback");
    }

    [Fact]
    public async Task GetValueOrDefaultAsyncReturnsFallbackOnFailure()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure<string>(FirstError));

        // Act
        var value = await resultTask.GetValueOrDefaultAsync("fallback");

        // Assert
        value.ShouldBe("fallback");
    }

    [Fact]
    public void TapErrorInvokesActionOnFailure()
    {
        // Arrange
        var result = Result.Failure(FirstError);
        Error? captured = null;

        // Act
        var tapped = result.TapError(error => captured = error);

        // Assert
        captured.ShouldBe(FirstError);
        tapped.ShouldBe(result);
    }

    [Fact]
    public void TapErrorSkipsActionOnSuccess()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var tapped = result.TapError(_ => throw new InvalidOperationException("should not run"));

        // Assert
        tapped.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void MapErrorTransformsErrorOnFailure()
    {
        // Arrange
        var result = Result.Failure(FirstError);

        // Act
        var mapped = result.MapError(_ => SecondError);

        // Assert
        mapped.IsFailure.ShouldBeTrue();
        mapped.Error.ShouldBe(SecondError);
    }

    [Fact]
    public void MapErrorLeavesSuccessUnchanged()
    {
        // Arrange
        var result = Result.Success();

        // Act
        var mapped = result.MapError(_ => throw new InvalidOperationException("should not run"));

        // Assert
        mapped.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task MapErrorAsyncTransformsErrorOnTaskSource()
    {
        // Arrange
        var resultTask = Task.FromResult(Result.Failure(FirstError));

        // Act
        var mapped = await resultTask.MapErrorAsync(_ => Task.FromResult(SecondError));

        // Assert
        mapped.Error.ShouldBe(SecondError);
    }

    [Fact]
    public void RecoverProducesSuccessFromFailure()
    {
        // Arrange
        var result = Result.Failure<int>(FirstError);

        // Act
        var recovered = result.Recover(_ => 42);

        // Assert
        recovered.IsSuccess.ShouldBeTrue();
        recovered.Value.ShouldBe(42);
    }

    [Fact]
    public void RecoverLeavesSuccessUnchanged()
    {
        // Arrange
        var result = Result.Success(7);

        // Act
        var recovered = result.Recover(_ => throw new InvalidOperationException("should not run"));

        // Assert
        recovered.Value.ShouldBe(7);
    }

    [Fact]
    public void OrReturnsFallbackOnFailure()
    {
        // Arrange
        var result = Result.Failure<int>(FirstError);
        var fallback = Result.Success(99);

        // Act
        var combined = result.Or(fallback);

        // Assert
        combined.IsSuccess.ShouldBeTrue();
        combined.Value.ShouldBe(99);
    }

    [Fact]
    public void OrReturnsOriginalOnSuccess()
    {
        // Arrange
        var result = Result.Success(1);
        var fallback = Result.Success(99);

        // Act
        var combined = result.Or(fallback);

        // Assert
        combined.Value.ShouldBe(1);
    }

    [Fact]
    public void OrElseInvokesFallbackOnFailure()
    {
        // Arrange
        var result = Result.Failure<int>(FirstError);

        // Act
        var combined = result.OrElse(_ => Result.Success(123));

        // Assert
        combined.IsSuccess.ShouldBeTrue();
        combined.Value.ShouldBe(123);
    }

    [Fact]
    public void OrElseSkipsFallbackOnSuccess()
    {
        // Arrange
        var result = Result.Success(5);

        // Act
        var combined = result.OrElse(_ => throw new InvalidOperationException("should not run"));

        // Assert
        combined.Value.ShouldBe(5);
    }

    [Fact]
    public void CombineReturnsSuccessWhenAllSucceed()
    {
        // Act
        var combined = ResultExtensions.Combine(Result.Success(), Result.Success());

        // Assert
        combined.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CombineAggregatesFailedErrors()
    {
        // Arrange
        var results = new[] { Result.Success(), Result.Failure(FirstError), Result.Failure(SecondError) };

        // Act
        var combined = results.Combine();

        // Assert
        combined.IsFailure.ShouldBeTrue();
        combined.Error.ShouldBeOfType<ValidationError>()
            .Errors.ShouldBe([FirstError, SecondError]);
    }

    [Fact]
    public void CombinePropagatesOriginalErrorForSingleFailure()
    {
        // Arrange
        var results = new[] { Result.Success(), Result.Failure(FirstError) };

        // Act
        var combined = results.Combine();

        // Assert
        combined.IsFailure.ShouldBeTrue();
        combined.Error.ShouldBeSameAs(FirstError);
    }

    [Fact]
    public void CombineParamsPropagatesOriginalErrorForSingleFailure()
    {
        // Act
        var combined = ResultExtensions.Combine(Result.Success(), Result.Failure(FirstError));

        // Assert
        combined.IsFailure.ShouldBeTrue();
        combined.Error.ShouldBeSameAs(FirstError);
    }

    [Fact]
    public async Task CombineAsyncPropagatesOriginalErrorForSingleFailure()
    {
        // Arrange
        var resultTasks = new[]
        {
            Task.FromResult(Result.Success()),
            Task.FromResult(Result.Failure(FirstError))
        };

        // Act
        var combined = await resultTasks.CombineAsync();

        // Assert
        combined.IsFailure.ShouldBeTrue();
        combined.Error.ShouldBeSameAs(FirstError);
    }

    [Fact]
    public async Task CombineAsyncAggregatesMultipleFailures()
    {
        // Arrange
        var resultTasks = new[]
        {
            Task.FromResult(Result.Failure(FirstError)),
            Task.FromResult(Result.Failure(SecondError))
        };

        // Act
        var combined = await resultTasks.CombineAsync();

        // Assert
        combined.Error.ShouldBeOfType<ValidationError>()
            .Errors.ShouldBe([FirstError, SecondError]);
    }

    [Fact]
    public void ZipCombinesTwoSuccessesIntoTuple()
    {
        // Arrange
        var first = Result.Success(1);
        var second = Result.Success("two");

        // Act
        var zipped = first.Zip(second);

        // Assert
        zipped.IsSuccess.ShouldBeTrue();
        zipped.Value.ShouldBe((1, "two"));
    }

    [Fact]
    public void ZipPropagatesSingleFailure()
    {
        // Arrange
        var first = Result.Failure<int>(FirstError);
        var second = Result.Success("two");

        // Act
        var zipped = first.Zip(second);

        // Assert
        zipped.IsFailure.ShouldBeTrue();
        zipped.Error.ShouldBe(FirstError);
    }

    [Fact]
    public void ZipAggregatesBothFailuresIntoValidationError()
    {
        // Arrange
        var first = Result.Failure<int>(FirstError);
        var second = Result.Failure<string>(SecondError);

        // Act
        var zipped = first.Zip(second);

        // Assert
        zipped.Error.ShouldBeOfType<ValidationError>()
            .Errors.ShouldBe([FirstError, SecondError]);
    }

    [Fact]
    public void ZipWithSelectorProjectsBothValues()
    {
        // Arrange
        var first = Result.Success(2);
        var second = Result.Success(3);

        // Act
        var zipped = first.Zip(second, (a, b) => a * b);

        // Assert
        zipped.IsSuccess.ShouldBeTrue();
        zipped.Value.ShouldBe(6);
    }

    [Fact]
    public async Task ZipAsyncCombinesAwaitedSources()
    {
        // Arrange
        var firstTask = Task.FromResult(Result.Success(1));
        var secondTask = Task.FromResult(Result.Success(2));

        // Act
        var zipped = await firstTask.ZipAsync(secondTask, (a, b) => a + b);

        // Assert
        zipped.Value.ShouldBe(3);
    }
}
