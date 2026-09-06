using Microsoft.Extensions.DependencyInjection;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests;

public sealed class ConsumerHostedServiceTests : BaseUnitTestCase
{
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);

    private readonly Lazy<ConsumerHostedService> _lazyTarget;
    private readonly TimerAwareFakeTimeProvider _timeProvider = new();

    private ConsumerHostedService Target => _lazyTarget.Value;

    public ConsumerHostedServiceTests()
    {
        _lazyTarget = new(CreateInstance<ConsumerHostedService>);
        Use<TimeProvider>(_timeProvider);
    }

    private void UseTransportProvider(Func<IServiceProvider, ITransport> transportFactory)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITransport>(transportFactory);
        var provider = services.BuildServiceProvider();

        Use<IServiceProvider>(provider);
        Use(provider.GetRequiredService<IServiceProviderIsService>());
    }

    [Fact]
    public async Task SuccessfulStartStartsTransportOnce()
    {
        // Arrange
        var transport = GetMock<ITransport>();
        transport.Setup(t => t.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        UseTransportProvider(_ => transport.Object);

        // Act
        await Target.StartAsync(CancellationToken);
        await Target.ExecuteTask!;

        // Assert
        Target.ExecuteTask.Status.ShouldBe(TaskStatus.RanToCompletion);
        transport.Verify(t => t.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransientStartupFailureIsRetriedUntilTransportStarts()
    {
        // Arrange
        var transport = GetMock<ITransport>();
        transport
            .SetupSequence(t => t.StartAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException())
            .Returns(Task.CompletedTask);
        UseTransportProvider(_ => transport.Object);

        // Act
        await Target.StartAsync(CancellationToken);
        var executeTask = Target.ExecuteTask!;
        await _timeProvider.TimerCreated.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        _timeProvider.Advance(InitialRetryDelay);
        await executeTask.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        // Assert
        executeTask.Status.ShouldBe(TaskStatus.RanToCompletion);
        transport.Verify(t => t.StartAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task StoppingDuringStartupRetryCompletesGracefully()
    {
        // Arrange
        var transport = GetMock<ITransport>();
        transport
            .Setup(t => t.StartAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unavailable"));
        UseTransportProvider(_ => transport.Object);
        await Target.StartAsync(CancellationToken);

        // Act
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBeOneOf(TaskStatus.RanToCompletion, TaskStatus.Canceled);
    }

    [Fact]
    public async Task TransportResolutionFailureIsRetriedInsteadOfFaultingHostedServiceConstruction()
    {
        // Arrange — the transport factory fails the first time it is resolved (e.g. an unreachable broker
        // connection), succeeding once the retry loop resolves it again.
        var attempts = 0;
        var transport = GetMock<ITransport>();
        transport.Setup(t => t.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        UseTransportProvider(_ => ++attempts == 1
            ? throw new InvalidOperationException("broker unreachable")
            : transport.Object);

        // Act — merely constructing the target must not throw, and starting it must reach the retry loop.
        await Target.StartAsync(CancellationToken);
        var executeTask = Target.ExecuteTask!;
        await _timeProvider.TimerCreated.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        _timeProvider.Advance(InitialRetryDelay);
        await executeTask.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        // Assert
        executeTask.Status.ShouldBe(TaskStatus.RanToCompletion);
        attempts.ShouldBe(2);
        transport.Verify(t => t.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MultipleRegisteredTransportsStartsOnlyTheLastRegisteredOne()
    {
        // Arrange — pins the "last registration wins" contract ResolveTransport documents: Vulthil.Messaging.TestHarness's
        // UseTestHarness()/ReplaceTransportWithTestHarness() remove any prior ITransport registration before adding
        // their own, so the in-memory transport is normally the only (and therefore last) one; if a transport is
        // ever registered again afterward, the most recently registered ITransport must be the one that starts.
        var services = new ServiceCollection();
        var firstRegistered = new Mock<ITransport>();
        firstRegistered.Setup(t => t.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var lastRegistered = new Mock<ITransport>();
        lastRegistered.Setup(t => t.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        services.AddSingleton(firstRegistered.Object);
        services.AddSingleton(lastRegistered.Object);
        var provider = services.BuildServiceProvider();

        Use<IServiceProvider>(provider);
        Use(provider.GetRequiredService<IServiceProviderIsService>());

        // Act
        await Target.StartAsync(CancellationToken);
        await Target.ExecuteTask!;

        // Assert
        Target.ExecuteTask.Status.ShouldBe(TaskStatus.RanToCompletion);
        lastRegistered.Verify(t => t.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        firstRegistered.Verify(t => t.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// A fake clock that reports when the service under test registers its first timer. The generic host runs
    /// <c>BackgroundService.ExecuteAsync</c> on the thread pool, so the retry delay is not yet pending when
    /// <c>StartAsync</c> returns; advancing the clock before the timer exists would leave the delay waiting forever.
    /// </summary>
    private sealed class TimerAwareFakeTimeProvider : FakeTimeProvider
    {
        private readonly TaskCompletionSource _timerCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task TimerCreated => _timerCreated.Task;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            _timerCreated.TrySetResult();
            return timer;
        }
    }
}
