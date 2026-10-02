using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.Extensions.Hosting;
using Vulthil.Extensions.Testing;

namespace Vulthil.xUnit.Tests;

public sealed class TestHostResetTests : BaseUnitTestCase
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly List<string> _log = [];
    private readonly Lazy<TestHostReset> _lazyTarget;

    private TestHostReset Target => _lazyTarget.Value;

    public TestHostResetTests()
    {
        _lazyTarget = new(() => new TestHostReset(_timeProvider));
    }

    private static ServiceProvider HostWith(params IHostedService[] services)
    {
        var collection = new ServiceCollection();
        foreach (var service in services)
        {
            collection.AddSingleton(service);
        }

        return collection.BuildServiceProvider();
    }

    private static readonly Func<CancellationToken, Task> Throw = _ => throw new InvalidOperationException("boom");

    private Task ResetAsync(IServiceProvider host, IReadOnlyCollection<IResettableResource> resources, params IResettableTestState[] testStates) =>
        Target.ResetAsync(TestHostReset.RestartableServicesOf(host), resources, host, testStates);

    public sealed class RecordingService(
        ICollection<string> log,
        string name,
        Func<CancellationToken, Task>? onStop = null,
        Func<CancellationToken, Task>? onStart = null) : IRestartableHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            log.Add($"start:{name}");
            return onStart?.Invoke(cancellationToken) ?? Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            log.Add($"stop:{name}");
            return onStop?.Invoke(cancellationToken) ?? Task.CompletedTask;
        }
    }

    public sealed class PlainService(ICollection<string> log) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            log.Add("start:plain");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            log.Add("stop:plain");
            return Task.CompletedTask;
        }
    }

    public sealed class RecordingResource(
        ICollection<string> log, string name, Func<Task>? onReset = null) : IResettableResource
    {
        public ValueTask ResetAsync(IServiceProvider serviceProvider)
        {
            log.Add($"reset:{name}");
            return onReset is null ? ValueTask.CompletedTask : new ValueTask(onReset());
        }
    }

    public sealed class RecordingTestState(
        ICollection<string> log, string name, Func<CancellationToken, Task>? onReset = null) : IResettableTestState
    {
        public CancellationToken ResetToken { get; private set; }

        public ValueTask ResetAsync(CancellationToken cancellationToken = default)
        {
            log.Add($"reset:{name}");
            ResetToken = cancellationToken;
            return onReset is null ? ValueTask.CompletedTask : new ValueTask(onReset(cancellationToken));
        }
    }

    [Fact]
    public async Task ResetStopsEveryServiceBeforeResettingAndRestartsThemAfterwards()
    {
        // Arrange
        await using var host = HostWith(new RecordingService(_log, "a"), new RecordingService(_log, "b"));

        // Act
        await ResetAsync(host, [new RecordingResource(_log, "db")]);

        // Assert
        _log.ShouldBe(["stop:a", "stop:b", "reset:db", "start:a", "start:b"]);
    }

    [Fact]
    public async Task AServiceThatIsNotRestartableIsLeftAlone()
    {
        // Arrange
        await using var host = HostWith(new PlainService(_log), new RecordingService(_log, "a"));

        // Act
        await ResetAsync(host, []);

        // Assert
        _log.ShouldBe(["stop:a", "start:a"]);
    }

    [Fact]
    public async Task AFailingStopStillResetsTheResourcesAndRestartsOnlyTheServicesThatStopped()
    {
        // Arrange
        await using var host = HostWith(new RecordingService(_log, "a", onStop: Throw), new RecordingService(_log, "b"));

        // Act
        var exception = await Should.ThrowAsync<AggregateException>(() => ResetAsync(host, [new RecordingResource(_log, "db")]));

        // Assert
        _log.ShouldBe(["stop:a", "stop:b", "reset:db", "start:b"]);
        var failure = exception.InnerExceptions.ShouldHaveSingleItem();
        failure.Message.ShouldContain("Stopping");
        failure.InnerException.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("boom");
    }

    [Fact]
    public async Task AFailingRestartStillRestartsTheOtherServicesAndReportsTheFailure()
    {
        // Arrange
        await using var host = HostWith(new RecordingService(_log, "a", onStart: Throw), new RecordingService(_log, "b"));

        // Act
        var exception = await Should.ThrowAsync<AggregateException>(() => ResetAsync(host, []));

        // Assert
        _log.ShouldBe(["stop:a", "stop:b", "start:a", "start:b"]);
        exception.InnerExceptions.ShouldHaveSingleItem().Message.ShouldContain("Restarting");
    }

    [Fact]
    public async Task AFailingResourceResetDoesNotSkipTheOtherResourcesOrTheRestarts()
    {
        // Arrange
        await using var host = HostWith(new RecordingService(_log, "a"));
        IResettableResource[] resources =
        [
            new RecordingResource(_log, "broken", () => Task.FromException(new InvalidOperationException("dirty"))),
            new RecordingResource(_log, "db"),
        ];

        // Act
        var exception = await Should.ThrowAsync<AggregateException>(() => ResetAsync(host, resources));

        // Assert
        _log.ShouldContain("reset:broken");
        _log.ShouldContain("reset:db");
        _log[^1].ShouldBe("start:a");
        var failure = exception.InnerExceptions.ShouldHaveSingleItem();
        failure.Message.ShouldContain("Resetting");
        failure.InnerException.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("dirty");
    }

    [Fact]
    public async Task ResetResetsTheTestStatesWhileTheServicesAreStopped()
    {
        // Arrange
        await using var host = HostWith(new RecordingService(_log, "a"));

        // Act
        await ResetAsync(host, [new RecordingResource(_log, "db")], new RecordingTestState(_log, "harness"));

        // Assert
        _log.ShouldBe(["stop:a", "reset:db", "reset:harness", "start:a"]);
    }

    [Fact]
    public async Task AFailingTestStateResetIsReportedAndTheServicesStillRestart()
    {
        // Arrange
        await using var host = HostWith(new RecordingService(_log, "a"));

        // Act
        var exception = await Should.ThrowAsync<AggregateException>(() => ResetAsync(host, [], new RecordingTestState(_log, "broken", Throw)));

        // Assert
        _log.ShouldBe(["stop:a", "reset:broken", "start:a"]);
        var failure = exception.InnerExceptions.ShouldHaveSingleItem();
        failure.Message.ShouldBe($"Resetting '{nameof(RecordingTestState)}' failed.");
        failure.InnerException.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("boom");
    }

    [Fact]
    public async Task AResourceResetThatNeverCompletesIsAbandonedAtTheStepTimeoutAndTheServicesRestart()
    {
        // Arrange
        var neverResets = new TaskCompletionSource();
        await using var host = HostWith(new RecordingService(_log, "a"));
        IResettableResource[] resources =
        [
            new RecordingResource(_log, "stuck", () => neverResets.Task),
            new RecordingResource(_log, "db"),
        ];

        // Act
        var reset = ResetAsync(host, resources);
        _timeProvider.Advance(TestHostReset.StepTimeout);
        var exception = await Should.ThrowAsync<AggregateException>(() => reset);

        // Assert
        _log.ShouldBe(["stop:a", "reset:stuck", "reset:db", "start:a"]);
        exception.InnerExceptions.ShouldHaveSingleItem().ShouldBeOfType<TimeoutException>()
            .Message.ShouldContain($"Resetting '{nameof(RecordingResource)}'");
    }

    [Fact]
    public async Task ATestStateResetThatHonoursItsTokenIsCancelledAtTheStepTimeout()
    {
        // Arrange
        await using var host = HostWith(new RecordingService(_log, "a"));
        var slowState = new RecordingTestState(_log, "slow", token => Task.Delay(Timeout.InfiniteTimeSpan, token));

        // Act
        var reset = ResetAsync(host, [], slowState);
        _timeProvider.Advance(TestHostReset.StepTimeout);
        var exception = await Should.ThrowAsync<AggregateException>(() => reset);

        // Assert
        _log.ShouldBe(["stop:a", "reset:slow", "start:a"]);
        exception.InnerExceptions.ShouldHaveSingleItem().ShouldBeOfType<TimeoutException>();
        slowState.ResetToken.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task AStopThatIgnoresItsTokenIsAbandonedAtTheStepTimeoutAndTheResetMovesOn()
    {
        // Arrange
        var neverStops = new TaskCompletionSource();
        await using var host = HostWith(new RecordingService(_log, "stuck", onStop: _ => neverStops.Task), new RecordingService(_log, "b"));

        // Act
        var reset = ResetAsync(host, [new RecordingResource(_log, "db")]);
        _timeProvider.Advance(TestHostReset.StepTimeout);
        var exception = await Should.ThrowAsync<AggregateException>(() => reset);

        // Assert
        _log.ShouldBe(["stop:stuck", "stop:b", "reset:db", "start:b"]);
        exception.InnerExceptions.ShouldHaveSingleItem().ShouldBeOfType<TimeoutException>().Message.ShouldContain("Stopping");
    }

    [Fact]
    public async Task AStopThatHonoursItsTokenIsCancelledAtTheStepTimeout()
    {
        // Arrange
        await using var host = HostWith(new RecordingService(_log, "slow", onStop: token => Task.Delay(Timeout.InfiniteTimeSpan, token)));

        // Act
        var reset = ResetAsync(host, []);
        _timeProvider.Advance(TestHostReset.StepTimeout);
        var exception = await Should.ThrowAsync<AggregateException>(() => reset);

        // Assert
        _log.ShouldBe(["stop:slow"]);
        exception.InnerExceptions.ShouldHaveSingleItem().ShouldBeOfType<TimeoutException>();
    }

    [Fact]
    public async Task AHostWithoutRestartableServicesOrResourcesResetsWithoutError()
    {
        // Arrange
        await using var host = HostWith();

        // Act & Assert
        await Should.NotThrowAsync(() => ResetAsync(host, []));
    }
}
