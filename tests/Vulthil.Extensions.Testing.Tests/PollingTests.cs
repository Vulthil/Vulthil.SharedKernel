using System.Diagnostics;
using Vulthil.Results;
using Vulthil.xUnit;

namespace Vulthil.Extensions.Testing.Tests;

public sealed class PollingWaitAsyncOfTTests : BaseUnitTestCase
{
    private static readonly TimeSpan ShortTick = TimeSpan.FromMilliseconds(15);

    [Fact]
    public async Task SucceedsOnTheFirstAttemptWithoutWaitingForATick()
    {
        // Arrange
        var callCount = 0;
        Task<Result<int>> Poll(CancellationToken ct)
        {
            callCount++;
            return Task.FromResult(Result.Success(42));
        }
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await Polling.WaitAsync(TimeSpan.FromSeconds(5), Poll, TimeSpan.FromSeconds(5), CancellationToken);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        callCount.ShouldBe(1);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task SucceedsOnALaterAttemptAfterInitialFailures()
    {
        // Arrange
        var attempt = 0;
        Task<Result<string>> Poll(CancellationToken ct)
        {
            attempt++;
            return Task.FromResult(attempt < 3
                ? Result.Failure<string>(Error.Failure($"attempt-{attempt}", "not yet"))
                : Result.Success("done"));
        }

        // Act
        var result = await Polling.WaitAsync(TimeSpan.FromSeconds(5), Poll, ShortTick, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("done");
        attempt.ShouldBe(3);
    }

    [Fact]
    public async Task TimingOutAggregatesEveryAttemptsErrorsInOrder()
    {
        // Arrange
        var attempt = 0;
        Task<Result<int>> Poll(CancellationToken ct)
        {
            attempt++;
            return Task.FromResult(Result.Failure<int>(Error.Failure($"error-{attempt}", "still failing")));
        }

        // Act
        var result = await Polling.WaitAsync(TimeSpan.FromMilliseconds(70), Poll, ShortTick, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.PollingError.ShouldNotBeNull();
        result.PollingError.Code.ShouldBe(Polling.Timeout.Code);
        result.PollingError.Description.ShouldBe(Polling.Timeout.Description);
        result.PollingError.Errors.Count.ShouldBe(attempt);
        result.PollingError.Errors.Select(error => error.Code)
            .ShouldBe(Enumerable.Range(1, attempt).Select(index => $"error-{index}"));
    }

    [Fact]
    public async Task TimingOutCompletesNormallyWithoutThrowing()
    {
        // Act
        var result = await Polling.WaitAsync<int>(
            TimeSpan.FromMilliseconds(40),
            _ => Task.FromResult(Result.Failure<int>(Error.Failure("nope", "nope"))),
            ShortTick,
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.PollingError.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExternalCancellationThrowsInsteadOfReturningATimeoutResult()
    {
        // Arrange
        using var externalCts = new CancellationTokenSource();
        var observedToken = default(CancellationToken);
        Task<Result<int>> Poll(CancellationToken ct)
        {
            observedToken = ct;
            return Task.FromResult(Result.Failure<int>(Error.Failure("still-going", "not yet")));
        }
        externalCts.CancelAfter(TimeSpan.FromMilliseconds(30));

        // Act
        var exception = await Should.ThrowAsync<OperationCanceledException>(
            () => Polling.WaitAsync(TimeSpan.FromSeconds(30), Poll, TimeSpan.FromMilliseconds(10), externalCts.Token));

        // Assert
        exception.ShouldNotBeNull();
        observedToken.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task NullFuncThrowsArgumentNullException()
    {
        // Arrange
        Func<CancellationToken, Task<Result<int>>>? func = null;

        // Act
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => Polling.WaitAsync(TimeSpan.FromSeconds(1), func!, CancellationToken));

        // Assert
        exception.ParamName.ShouldBe("func");
    }
}

public sealed class PollingWaitAsyncTests : BaseUnitTestCase
{
    private static readonly TimeSpan ShortTick = TimeSpan.FromMilliseconds(15);

    [Fact]
    public async Task SucceedsOnTheFirstAttemptWithoutWaitingForATick()
    {
        // Arrange
        var callCount = 0;
        Task<Result> Poll(CancellationToken ct)
        {
            callCount++;
            return Task.FromResult(Result.Success());
        }
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await Polling.WaitAsync(TimeSpan.FromSeconds(5), Poll, TimeSpan.FromSeconds(5), CancellationToken);
        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        callCount.ShouldBe(1);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task SucceedsOnALaterAttemptAfterInitialFailures()
    {
        // Arrange
        var attempt = 0;
        Task<Result> Poll(CancellationToken ct)
        {
            attempt++;
            return Task.FromResult(attempt < 3
                ? Result.Failure(Error.Failure($"attempt-{attempt}", "not yet"))
                : Result.Success());
        }

        // Act
        var result = await Polling.WaitAsync(TimeSpan.FromSeconds(5), Poll, ShortTick, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        attempt.ShouldBe(3);
    }

    [Fact]
    public async Task TimingOutAggregatesEveryAttemptsErrorsInOrder()
    {
        // Arrange
        var attempt = 0;
        Task<Result> Poll(CancellationToken ct)
        {
            attempt++;
            return Task.FromResult(Result.Failure(Error.Failure($"error-{attempt}", "still failing")));
        }

        // Act
        var result = await Polling.WaitAsync(TimeSpan.FromMilliseconds(70), Poll, ShortTick, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.PollingError.ShouldNotBeNull();
        result.PollingError.Code.ShouldBe(Polling.Timeout.Code);
        result.PollingError.Description.ShouldBe(Polling.Timeout.Description);
        result.PollingError.Errors.Count.ShouldBe(attempt);
        result.PollingError.Errors.Select(error => error.Code)
            .ShouldBe(Enumerable.Range(1, attempt).Select(index => $"error-{index}"));
    }

    [Fact]
    public async Task TimingOutCompletesNormallyWithoutThrowing()
    {
        // Act
        var result = await Polling.WaitAsync(
            TimeSpan.FromMilliseconds(40),
            _ => Task.FromResult(Result.Failure(Error.Failure("nope", "nope"))),
            ShortTick,
            CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.PollingError.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExternalCancellationThrowsInsteadOfReturningATimeoutResult()
    {
        // Arrange
        using var externalCts = new CancellationTokenSource();
        var observedToken = default(CancellationToken);
        Task<Result> Poll(CancellationToken ct)
        {
            observedToken = ct;
            return Task.FromResult(Result.Failure(Error.Failure("still-going", "not yet")));
        }
        externalCts.CancelAfter(TimeSpan.FromMilliseconds(30));

        // Act
        var exception = await Should.ThrowAsync<OperationCanceledException>(
            () => Polling.WaitAsync(TimeSpan.FromSeconds(30), Poll, TimeSpan.FromMilliseconds(10), externalCts.Token));

        // Assert
        exception.ShouldNotBeNull();
        observedToken.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task NullFuncThrowsArgumentNullException()
    {
        // Arrange
        Func<CancellationToken, Task<Result>>? func = null;

        // Act
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => Polling.WaitAsync(TimeSpan.FromSeconds(1), func!, CancellationToken));

        // Assert
        exception.ParamName.ShouldBe("func");
    }
}
