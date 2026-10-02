using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests;

public sealed class ConsumerHostedServiceTests : BaseUnitTestCase
{
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);

    private readonly Lazy<ConsumerHostedService> _lazyTarget;
    private readonly TimerAwareFakeTimeProvider _timeProvider = new();
    private readonly Channel<bool> _transportStarts = Channel.CreateUnbounded<bool>();

    private ConsumerHostedService Target => _lazyTarget.Value;

    public ConsumerHostedServiceTests()
    {
        _lazyTarget = new(CreateInstance<ConsumerHostedService>);
        Use<TimeProvider>(_timeProvider);
    }

    protected override async ValueTask Dispose()
    {
        if (_lazyTarget.IsValueCreated)
        {
            Target.Dispose();
        }

        await base.Dispose();
    }

    [Fact]
    public async Task SuccessfulStartStartsTransportOnce()
    {
        // Arrange
        var transport = UseStartingTransport();

        // Act
        await Target.StartAsync(CancellationToken);
        await WaitForTransportStartAsync();

        // Assert
        transport.Verify(t => t.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        transport.Verify(t => t.StopAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TransientStartupFailureIsRetriedUntilTransportStarts()
    {
        // Arrange
        var attempts = 0;
        var transport = GetMock<ITransport>();
        transport
            .Setup(t => t.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ++attempts == 1 ? Task.FromException(new TaskCanceledException()) : SignalTransportStart());
        UseTransportProvider(_ => transport.Object);

        // Act
        await Target.StartAsync(CancellationToken);
        await _timeProvider.TimerCreated.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        _timeProvider.Advance(InitialRetryDelay);
        await WaitForTransportStartAsync();

        // Assert
        transport.Verify(t => t.StartAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task StoppingDuringStartupRetryCompletesGracefullyWithoutStoppingTheTransport()
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
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
        transport.Verify(t => t.StopAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TransportResolutionFailureIsRetriedInsteadOfFaultingHostedServiceConstruction()
    {
        // Arrange
        var attempts = 0;
        var transport = GetMock<ITransport>();
        transport.Setup(t => t.StartAsync(It.IsAny<CancellationToken>())).Returns(() => SignalTransportStart());
        UseTransportProvider(_ => ++attempts == 1
            ? throw new InvalidOperationException("broker unreachable")
            : transport.Object);

        // Act
        await Target.StartAsync(CancellationToken);
        await _timeProvider.TimerCreated.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        _timeProvider.Advance(InitialRetryDelay);
        await WaitForTransportStartAsync();

        // Assert
        attempts.ShouldBe(2);
        transport.Verify(t => t.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MultipleRegisteredTransportsStartsOnlyTheLastRegisteredOne()
    {
        // Arrange
        var services = new ServiceCollection();
        var firstRegistered = new Mock<ITransport>();
        firstRegistered.Setup(t => t.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var lastRegistered = new Mock<ITransport>();
        lastRegistered.Setup(t => t.StartAsync(It.IsAny<CancellationToken>())).Returns(() => SignalTransportStart());
        services.AddSingleton(firstRegistered.Object);
        services.AddSingleton(lastRegistered.Object);
        var provider = services.BuildServiceProvider();

        Use<IServiceProvider>(provider);
        Use(provider.GetRequiredService<IServiceProviderIsService>());

        // Act
        await Target.StartAsync(CancellationToken);
        await WaitForTransportStartAsync();

        // Assert
        lastRegistered.Verify(t => t.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        firstRegistered.Verify(t => t.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StoppingStopsTheStartedTransport()
    {
        // Arrange
        var transport = UseStartingTransport();
        await Target.StartAsync(CancellationToken);
        await WaitForTransportStartAsync();

        // Act
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
        transport.Verify(t => t.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestartingStartsTheTransportAgain()
    {
        // Arrange
        var transport = UseStartingTransport();
        await Target.StartAsync(CancellationToken);
        await WaitForTransportStartAsync();
        await Target.StopAsync(CancellationToken);

        // Act
        await Target.StartAsync(CancellationToken);
        await WaitForTransportStartAsync();

        // Assert
        transport.Verify(t => t.StartAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        transport.Verify(t => t.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ARestartWaitsForThePreviousTransportStopBeforeStartingTheTransportAgain()
    {
        // Arrange
        var stopRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transport = UseStartingTransport();
        transport.Setup(t => t.StopAsync(It.IsAny<CancellationToken>())).Returns(stopRelease.Task);
        await Target.StartAsync(CancellationToken);
        await WaitForTransportStartAsync();
        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Target.StopAsync(stopTimeout.Token);

        // Act
        var restart = Target.StartAsync(CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken);
        var startsWhileTheStopRuns = transport.Invocations.Count(invocation => invocation.Method.Name == nameof(ITransport.StartAsync));
        stopRelease.SetResult();
        await restart.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
        await WaitForTransportStartAsync();

        // Assert
        startsWhileTheStopRuns.ShouldBe(1);
        transport.Verify(t => t.StartAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task AFailedTransportStopIsLoggedWithoutStoppingTheApplication()
    {
        // Arrange
        var transport = UseStartingTransport();
        transport
            .Setup(t => t.StopAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("channel already closed"));
        await Target.StartAsync(CancellationToken);
        await WaitForTransportStartAsync();

        // Act
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
        GetMock<IHostApplicationLifetime>().Verify(lifetime => lifetime.StopApplication(), Times.Never);
    }

    private Mock<ITransport> UseStartingTransport()
    {
        var transport = GetMock<ITransport>();
        transport.Setup(t => t.StartAsync(It.IsAny<CancellationToken>())).Returns(() => SignalTransportStart());
        UseTransportProvider(_ => transport.Object);
        return transport;
    }

    private void UseTransportProvider(Func<IServiceProvider, ITransport> transportFactory)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITransport>(transportFactory);
        var provider = services.BuildServiceProvider();

        Use<IServiceProvider>(provider);
        Use(provider.GetRequiredService<IServiceProviderIsService>());
    }

    private Task SignalTransportStart()
    {
        _transportStarts.Writer.TryWrite(true);
        return Task.CompletedTask;
    }

    private async Task WaitForTransportStartAsync() =>
        await _transportStarts.Reader.ReadAsync(CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);

    /// <summary>
    /// A fake clock that reports when the service under test registers its first timer. The service runs each
    /// generation on the thread pool, so the retry delay is not yet pending when <c>StartAsync</c> returns; advancing
    /// the clock before the timer exists would leave the delay waiting forever.
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
