using Vulthil.Results;
using Vulthil.xUnit;

namespace Vulthil.Extensions.Testing.Tests;

public sealed class PollingWaitAsyncOfTTests : BaseUnitTestCase
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(5);

    private readonly FakeTimeProvider _timeProvider = new();
    private TaskCompletionSource? _nextAttempt;

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

        // Act
        var polling = Polling.WaitAsync(TimeSpan.FromSeconds(5), Poll, TimeSpan.FromSeconds(5), CancellationToken);
        var completedWithoutATick = polling.IsCompleted;
        var result = await polling;

        // Assert
        completedWithoutATick.ShouldBeTrue();
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        callCount.ShouldBe(1);
    }

    [Fact]
    public async Task SucceedsOnALaterAttemptAfterInitialFailures()
    {
        // Arrange
        var attempt = 0;
        Task<Result<string>> Poll(CancellationToken ct)
        {
            attempt++;
            RecordAttempt();
            return Task.FromResult(attempt < 3
                ? Result.Failure<string>(Error.Failure($"attempt-{attempt}", "not yet"))
                : Result.Success("done"));
        }

        // Act
        var polling = Polling.WaitAsync(Tick * 5, Poll, Tick, _timeProvider, CancellationToken);
        await AdvanceToTheNextAttemptAsync();
        await AdvanceToTheNextAttemptAsync();
        var result = await polling.WaitAsync(Guard, CancellationToken);

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
            RecordAttempt();
            return Task.FromResult(Result.Failure<int>(Error.Failure($"error-{attempt}", "still failing")));
        }

        // Act
        var polling = Polling.WaitAsync(Tick * 2.5, Poll, Tick, _timeProvider, CancellationToken);
        await AdvanceToTheNextAttemptAsync();
        await AdvanceToTheNextAttemptAsync();
        _timeProvider.Advance(Tick / 2);
        var result = await polling.WaitAsync(Guard, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.PollingError.ShouldNotBeNull();
        result.PollingError.Code.ShouldBe(Polling.Timeout.Code);
        result.PollingError.Description.ShouldBe(Polling.Timeout.Description);
        result.PollingError.Errors.Select(error => error.Code).ShouldBe(["error-1", "error-2", "error-3"]);
    }

    [Fact]
    public async Task TimingOutCompletesNormallyWithoutThrowing()
    {
        // Act
        var polling = Polling.WaitAsync<int>(
            Tick / 2,
            _ => Task.FromResult(Result.Failure<int>(Error.Failure("nope", "nope"))),
            Tick,
            _timeProvider,
            CancellationToken.None);
        _timeProvider.Advance(Tick / 2);
        var result = await polling.WaitAsync(Guard, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.PollingError.ShouldNotBeNull();
    }

    [Fact]
    public async Task TimingOutCancelsTheTokenTheFuncReceived()
    {
        // Arrange
        var observedToken = default(CancellationToken);
        Task<Result<int>> Poll(CancellationToken ct)
        {
            observedToken = ct;
            return Task.FromResult(Result.Failure<int>(Error.Failure("nope", "nope")));
        }

        // Act
        var polling = Polling.WaitAsync(Tick / 2, Poll, Tick, _timeProvider, CancellationToken);
        _timeProvider.Advance(Tick / 2);
        await polling.WaitAsync(Guard, CancellationToken);

        // Assert
        observedToken.IsCancellationRequested.ShouldBeTrue();
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

        // Act
        var polling = Polling.WaitAsync(TimeSpan.FromSeconds(30), Poll, TimeSpan.FromSeconds(30), externalCts.Token);
        await externalCts.CancelAsync();
        var exception = await Should.ThrowAsync<OperationCanceledException>(polling);

        // Assert
        exception.ShouldNotBeNull();
        observedToken.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task CallerCancellationWinsOverAnElapsedTimeout()
    {
        // Arrange
        using var callerCts = new CancellationTokenSource();
        async Task<Result<int>> Poll(CancellationToken ct)
        {
            await callerCts.CancelAsync();
            _timeProvider.Advance(Tick);
            return Result.Failure<int>(Error.Failure("too-late", "not yet"));
        }

        // Act
        var exception = await Should.ThrowAsync<OperationCanceledException>(
            () => Polling.WaitAsync(Tick / 2, Poll, Tick, _timeProvider, callerCts.Token).WaitAsync(Guard, CancellationToken));

        // Assert
        exception.ShouldNotBeNull();
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

    [Fact]
    public async Task NullFuncWithoutATokenThrowsArgumentNullExceptionFromEveryOverload()
    {
        // Arrange
        Func<Task<Result<int>>>? func = null;

        // Act
        ArgumentNullException[] exceptions =
        [
            await Should.ThrowAsync<ArgumentNullException>(() => Polling.WaitAsync(TimeSpan.FromSeconds(1), func!)),
            await Should.ThrowAsync<ArgumentNullException>(() => Polling.WaitAsync(TimeSpan.FromSeconds(1), func!, CancellationToken)),
            await Should.ThrowAsync<ArgumentNullException>(() => Polling.WaitAsync(TimeSpan.FromSeconds(1), func!, Tick, CancellationToken)),
        ];

        // Assert
        exceptions.ShouldAllBe(exception => exception.ParamName == "func");
    }

    [Fact]
    public async Task OptionsSetTheTimeoutTheTickAndTheClock()
    {
        // Arrange
        var attempt = 0;
        Task<Result<int>> Poll(CancellationToken ct)
        {
            attempt++;
            RecordAttempt();
            return Task.FromResult(Result.Failure<int>(Error.Failure($"error-{attempt}", "still failing")));
        }
        var options = new PollingOptions(Tick * 3) { TimerTick = Tick * 2, TimeProvider = _timeProvider };

        // Act
        var polling = Polling.WaitAsync(options, Poll, CancellationToken);
        _timeProvider.Advance(Tick);
        await AdvanceToTheNextAttemptAsync();
        _timeProvider.Advance(Tick);
        var result = await polling.WaitAsync(Guard, CancellationToken);

        // Assert
        result.PollingError.ShouldNotBeNull();
        result.PollingError.Errors.Select(error => error.Code).ShouldBe(["error-1", "error-2"]);
    }

    [Fact]
    public async Task NullOptionsThrowsArgumentNullException()
    {
        // Arrange
        PollingOptions? options = null;

        // Act
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => Polling.WaitAsync(options!, _ => Task.FromResult(Result.Success(42)), CancellationToken));

        // Assert
        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public async Task NullTimeProviderThrowsArgumentNullException()
    {
        // Arrange
        TimeProvider? timeProvider = null;

        // Act
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => Polling.WaitAsync(TimeSpan.FromSeconds(1), _ => Task.FromResult(Result.Success(42)), Tick, timeProvider!, CancellationToken));

        // Assert
        exception.ParamName.ShouldBe("timeProvider");
    }

    [Fact]
    public async Task ExceptionsTheFilterAcceptsCountAsFailedAttempts()
    {
        // Arrange
        var attempt = 0;
        List<Exception> thrown = [];
        Task<Result<int>> Poll(CancellationToken ct)
        {
            attempt++;
            RecordAttempt();
            if (attempt == 2)
            {
                return Task.FromResult(Result.Failure<int>(Error.Failure("error-2", "still failing")));
            }

            var exception = new HttpRequestException($"down-{attempt}");
            thrown.Add(exception);
            throw exception;
        }
        var options = new PollingOptions(Tick * 2.5)
        {
            TimerTick = Tick,
            TimeProvider = _timeProvider,
            TreatAsFailedAttempt = exception => exception is HttpRequestException,
        };

        // Act
        var polling = Polling.WaitAsync(options, Poll, CancellationToken);
        await AdvanceToTheNextAttemptAsync();
        await AdvanceToTheNextAttemptAsync();
        _timeProvider.Advance(Tick / 2);
        var result = await polling.WaitAsync(Guard, CancellationToken);

        // Assert
        result.PollingError.ShouldNotBeNull();
        result.PollingError.Errors.Select(error => error.Code)
            .ShouldBe(["Polling.Exception", "error-2", "Polling.Exception"]);
        result.PollingError.Errors.OfType<PollingExceptionError>().Select(error => error.Exception).ShouldBe(thrown);
    }

    [Fact]
    public async Task ExceptionsTheFilterRejectsEndThePoll()
    {
        // Arrange
        var thrown = new InvalidOperationException("broken probe");
        var options = new PollingOptions(Tick * 5)
        {
            TimerTick = Tick,
            TimeProvider = _timeProvider,
            TreatAsFailedAttempt = exception => exception is HttpRequestException,
        };

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => Polling.WaitAsync<int>(options, _ => throw thrown, CancellationToken).WaitAsync(Guard, CancellationToken));

        // Assert
        exception.ShouldBeSameAs(thrown);
    }

    [Fact]
    public async Task ExceptionsEndThePollWithoutAFilter()
    {
        // Arrange
        var thrown = new HttpRequestException("down");
        var options = new PollingOptions(Tick * 5) { TimerTick = Tick, TimeProvider = _timeProvider };

        // Act
        var exception = await Should.ThrowAsync<HttpRequestException>(
            () => Polling.WaitAsync<int>(options, _ => throw thrown, CancellationToken).WaitAsync(Guard, CancellationToken));

        // Assert
        exception.ShouldBeSameAs(thrown);
    }

    [Fact]
    public async Task CancellationByTheTimeoutIsNeverAFailedAttempt()
    {
        // Arrange
        var attempt = 0;
        async Task<Result<int>> Poll(CancellationToken ct)
        {
            attempt++;
            RecordAttempt();
            if (attempt == 1)
            {
                return Result.Failure<int>(Error.Failure("error-1", "still failing"));
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return Result.Success(42);
        }
        var options = new PollingOptions(Tick * 1.5)
        {
            TimerTick = Tick,
            TimeProvider = _timeProvider,
            TreatAsFailedAttempt = _ => true,
        };

        // Act
        var polling = Polling.WaitAsync(options, Poll, CancellationToken);
        await AdvanceToTheNextAttemptAsync();
        _timeProvider.Advance(Tick / 2);
        var result = await polling.WaitAsync(Guard, CancellationToken);

        // Assert
        result.PollingError.ShouldNotBeNull();
        result.PollingError.Errors.Select(error => error.Code).ShouldBe(["error-1"]);
    }

    [Fact]
    public async Task ACancellationThePollDidNotCauseCountsAsAFailedAttempt()
    {
        // Arrange
        var attempt = 0;
        Task<Result<int>> Poll(CancellationToken ct)
        {
            attempt++;
            RecordAttempt();
            return attempt == 1
                ? throw new TaskCanceledException("The HTTP request timed out.")
                : Task.FromResult(Result.Success(42));
        }
        var options = new PollingOptions(Tick * 5)
        {
            TimerTick = Tick,
            TimeProvider = _timeProvider,
            TreatAsFailedAttempt = exception => exception is TaskCanceledException,
        };

        // Act
        var polling = Polling.WaitAsync(options, Poll, CancellationToken);
        await AdvanceToTheNextAttemptAsync();
        var result = await polling.WaitAsync(Guard, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        attempt.ShouldBe(2);
    }

    private void RecordAttempt() => _nextAttempt?.TrySetResult();

    private async Task AdvanceToTheNextAttemptAsync()
    {
        var nextAttempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _nextAttempt = nextAttempt;
        _timeProvider.Advance(Tick);
        await nextAttempt.Task.WaitAsync(Guard, CancellationToken);
    }
}

public sealed class PollingWaitAsyncTests : BaseUnitTestCase
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(5);

    private readonly FakeTimeProvider _timeProvider = new();
    private TaskCompletionSource? _nextAttempt;

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

        // Act
        var polling = Polling.WaitAsync(TimeSpan.FromSeconds(5), Poll, TimeSpan.FromSeconds(5), CancellationToken);
        var completedWithoutATick = polling.IsCompleted;
        var result = await polling;

        // Assert
        completedWithoutATick.ShouldBeTrue();
        result.IsSuccess.ShouldBeTrue();
        callCount.ShouldBe(1);
    }

    [Fact]
    public async Task SucceedsOnALaterAttemptAfterInitialFailures()
    {
        // Arrange
        var attempt = 0;
        Task<Result> Poll(CancellationToken ct)
        {
            attempt++;
            RecordAttempt();
            return Task.FromResult(attempt < 3
                ? Result.Failure(Error.Failure($"attempt-{attempt}", "not yet"))
                : Result.Success());
        }

        // Act
        var polling = Polling.WaitAsync(Tick * 5, Poll, Tick, _timeProvider, CancellationToken);
        await AdvanceToTheNextAttemptAsync();
        await AdvanceToTheNextAttemptAsync();
        var result = await polling.WaitAsync(Guard, CancellationToken);

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
            RecordAttempt();
            return Task.FromResult(Result.Failure(Error.Failure($"error-{attempt}", "still failing")));
        }

        // Act
        var polling = Polling.WaitAsync(Tick * 2.5, Poll, Tick, _timeProvider, CancellationToken);
        await AdvanceToTheNextAttemptAsync();
        await AdvanceToTheNextAttemptAsync();
        _timeProvider.Advance(Tick / 2);
        var result = await polling.WaitAsync(Guard, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.PollingError.ShouldNotBeNull();
        result.PollingError.Code.ShouldBe(Polling.Timeout.Code);
        result.PollingError.Description.ShouldBe(Polling.Timeout.Description);
        result.PollingError.Errors.Select(error => error.Code).ShouldBe(["error-1", "error-2", "error-3"]);
    }

    [Fact]
    public async Task TimingOutCompletesNormallyWithoutThrowing()
    {
        // Act
        var polling = Polling.WaitAsync(
            Tick / 2,
            _ => Task.FromResult(Result.Failure(Error.Failure("nope", "nope"))),
            Tick,
            _timeProvider,
            CancellationToken.None);
        _timeProvider.Advance(Tick / 2);
        var result = await polling.WaitAsync(Guard, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.PollingError.ShouldNotBeNull();
    }

    [Fact]
    public async Task TimingOutCancelsTheTokenTheFuncReceived()
    {
        // Arrange
        var observedToken = default(CancellationToken);
        Task<Result> Poll(CancellationToken ct)
        {
            observedToken = ct;
            return Task.FromResult(Result.Failure(Error.Failure("nope", "nope")));
        }

        // Act
        var polling = Polling.WaitAsync(Tick / 2, Poll, Tick, _timeProvider, CancellationToken);
        _timeProvider.Advance(Tick / 2);
        await polling.WaitAsync(Guard, CancellationToken);

        // Assert
        observedToken.IsCancellationRequested.ShouldBeTrue();
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

        // Act
        var polling = Polling.WaitAsync(TimeSpan.FromSeconds(30), Poll, TimeSpan.FromSeconds(30), externalCts.Token);
        await externalCts.CancelAsync();
        var exception = await Should.ThrowAsync<OperationCanceledException>(polling);

        // Assert
        exception.ShouldNotBeNull();
        observedToken.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task CallerCancellationWinsOverAnElapsedTimeout()
    {
        // Arrange
        using var callerCts = new CancellationTokenSource();
        async Task<Result> Poll(CancellationToken ct)
        {
            await callerCts.CancelAsync();
            _timeProvider.Advance(Tick);
            return Result.Failure(Error.Failure("too-late", "not yet"));
        }

        // Act
        var exception = await Should.ThrowAsync<OperationCanceledException>(
            () => Polling.WaitAsync(Tick / 2, Poll, Tick, _timeProvider, callerCts.Token).WaitAsync(Guard, CancellationToken));

        // Assert
        exception.ShouldNotBeNull();
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

    [Fact]
    public async Task NullFuncWithoutATokenThrowsArgumentNullExceptionFromEveryOverload()
    {
        // Arrange
        Func<Task<Result>>? func = null;

        // Act
        ArgumentNullException[] exceptions =
        [
            await Should.ThrowAsync<ArgumentNullException>(() => Polling.WaitAsync(TimeSpan.FromSeconds(1), func!)),
            await Should.ThrowAsync<ArgumentNullException>(() => Polling.WaitAsync(TimeSpan.FromSeconds(1), func!, CancellationToken)),
            await Should.ThrowAsync<ArgumentNullException>(() => Polling.WaitAsync(TimeSpan.FromSeconds(1), func!, Tick, CancellationToken)),
        ];

        // Assert
        exceptions.ShouldAllBe(exception => exception.ParamName == "func");
    }

    [Fact]
    public async Task OptionsSetTheTimeoutTheTickAndTheClock()
    {
        // Arrange
        var attempt = 0;
        Task<Result> Poll(CancellationToken ct)
        {
            attempt++;
            RecordAttempt();
            return Task.FromResult(Result.Failure(Error.Failure($"error-{attempt}", "still failing")));
        }
        var options = new PollingOptions(Tick * 3) { TimerTick = Tick * 2, TimeProvider = _timeProvider };

        // Act
        var polling = Polling.WaitAsync(options, Poll, CancellationToken);
        _timeProvider.Advance(Tick);
        await AdvanceToTheNextAttemptAsync();
        _timeProvider.Advance(Tick);
        var result = await polling.WaitAsync(Guard, CancellationToken);

        // Assert
        result.PollingError.ShouldNotBeNull();
        result.PollingError.Errors.Select(error => error.Code).ShouldBe(["error-1", "error-2"]);
    }

    [Fact]
    public async Task NullOptionsThrowsArgumentNullException()
    {
        // Arrange
        PollingOptions? options = null;

        // Act
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => Polling.WaitAsync(options!, _ => Task.FromResult(Result.Success()), CancellationToken));

        // Assert
        exception.ParamName.ShouldBe("options");
    }

    [Fact]
    public async Task NullTimeProviderThrowsArgumentNullException()
    {
        // Arrange
        TimeProvider? timeProvider = null;

        // Act
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => Polling.WaitAsync(TimeSpan.FromSeconds(1), _ => Task.FromResult(Result.Success()), Tick, timeProvider!, CancellationToken));

        // Assert
        exception.ParamName.ShouldBe("timeProvider");
    }

    [Fact]
    public async Task ExceptionsTheFilterAcceptsCountAsFailedAttempts()
    {
        // Arrange
        var attempt = 0;
        List<Exception> thrown = [];
        Task<Result> Poll(CancellationToken ct)
        {
            attempt++;
            RecordAttempt();
            if (attempt == 2)
            {
                return Task.FromResult(Result.Failure(Error.Failure("error-2", "still failing")));
            }

            var exception = new HttpRequestException($"down-{attempt}");
            thrown.Add(exception);
            throw exception;
        }
        var options = new PollingOptions(Tick * 2.5)
        {
            TimerTick = Tick,
            TimeProvider = _timeProvider,
            TreatAsFailedAttempt = exception => exception is HttpRequestException,
        };

        // Act
        var polling = Polling.WaitAsync(options, Poll, CancellationToken);
        await AdvanceToTheNextAttemptAsync();
        await AdvanceToTheNextAttemptAsync();
        _timeProvider.Advance(Tick / 2);
        var result = await polling.WaitAsync(Guard, CancellationToken);

        // Assert
        result.PollingError.ShouldNotBeNull();
        result.PollingError.Errors.Select(error => error.Code)
            .ShouldBe(["Polling.Exception", "error-2", "Polling.Exception"]);
        result.PollingError.Errors.OfType<PollingExceptionError>().Select(error => error.Exception).ShouldBe(thrown);
    }

    [Fact]
    public async Task ConditionSucceedsOnTheFirstCheckWithoutWaitingForATick()
    {
        // Act
        var polling = Polling.WaitAsync(TimeSpan.FromSeconds(5), _ => Task.FromResult(true), CancellationToken);
        var completedWithoutATick = polling.IsCompleted;
        var result = await polling;

        // Assert
        completedWithoutATick.ShouldBeTrue();
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ConditionTimesOutWithAConditionNotMetErrorForEachCheck()
    {
        // Arrange
        Task<bool> Check(CancellationToken ct)
        {
            RecordAttempt();
            return Task.FromResult(false);
        }
        var options = new PollingOptions(Tick * 2.5) { TimerTick = Tick, TimeProvider = _timeProvider };

        // Act
        var polling = Polling.WaitAsync(options, Check, CancellationToken);
        await AdvanceToTheNextAttemptAsync();
        await AdvanceToTheNextAttemptAsync();
        _timeProvider.Advance(Tick / 2);
        var result = await polling.WaitAsync(Guard, CancellationToken);

        // Assert
        result.PollingError.ShouldNotBeNull();
        result.PollingError.Errors.Count.ShouldBe(3);
        result.PollingError.Errors.ShouldAllBe(error => error == Polling.ConditionNotMet);
    }

    [Fact]
    public async Task ConditionOptionsTreatAcceptedExceptionsAsFailedAttempts()
    {
        // Arrange
        var check = 0;
        Task<bool> Check(CancellationToken ct)
        {
            check++;
            RecordAttempt();
            return check == 1 ? throw new HttpRequestException("down") : Task.FromResult(true);
        }
        var options = new PollingOptions(Tick * 5)
        {
            TimerTick = Tick,
            TimeProvider = _timeProvider,
            TreatAsFailedAttempt = exception => exception is HttpRequestException,
        };

        // Act
        var polling = Polling.WaitAsync(options, Check, CancellationToken);
        await AdvanceToTheNextAttemptAsync();
        var result = await polling.WaitAsync(Guard, CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        check.ShouldBe(2);
    }

    [Fact]
    public async Task NullConditionThrowsArgumentNullException()
    {
        // Arrange
        Func<CancellationToken, Task<bool>>? func = null;

        // Act
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => Polling.WaitAsync(TimeSpan.FromSeconds(1), func!, CancellationToken));

        // Assert
        exception.ParamName.ShouldBe("func");
    }

    [Fact]
    public async Task NullOptionsThrowsArgumentNullExceptionForACondition()
    {
        // Arrange
        PollingOptions? options = null;

        // Act
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => Polling.WaitAsync(options!, _ => Task.FromResult(true), CancellationToken));

        // Assert
        exception.ParamName.ShouldBe("options");
    }

    private void RecordAttempt() => _nextAttempt?.TrySetResult();

    private async Task AdvanceToTheNextAttemptAsync()
    {
        var nextAttempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _nextAttempt = nextAttempt;
        _timeProvider.Advance(Tick);
        await nextAttempt.Task.WaitAsync(Guard, CancellationToken);
    }
}
