using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vulthil.xUnit;

namespace Vulthil.Extensions.Hosting.Tests;

public sealed class RestartableBackgroundServiceTests : BaseUnitTestCase
{
    private readonly Lazy<ProbeService> _lazyTarget;

    private ProbeService Target => _lazyTarget.Value;

    public RestartableBackgroundServiceTests() => _lazyTarget = new(CreateInstance<ProbeService>);

    protected override async ValueTask Dispose()
    {
        if (_lazyTarget.IsValueCreated)
        {
            Target.Dispose();
        }

        await base.Dispose();
    }

    [Fact]
    public async Task StoppingWhileAGenerationRunsCompletesGracefully()
    {
        // Arrange
        await Target.StartAsync(CancellationToken);
        await Target.WaitForGenerationAsync(CancellationToken);

        // Act
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
    }

    [Fact]
    public async Task StoppingBeforeTheGenerationHasRunCompletesGracefully()
    {
        // Arrange
        await Target.StartAsync(CancellationToken);

        // Act
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
    }

    [Fact]
    public async Task StartingWithAnAlreadyCanceledTokenCompletesTheExecuteTaskGracefully()
    {
        // Arrange
        using var canceledTokenSource = new CancellationTokenSource();
        await canceledTokenSource.CancelAsync();

        // Act
        await Target.StartAsync(canceledTokenSource.Token);
        await Target.ExecuteTask!;

        // Assert
        Target.ExecuteTask.Status.ShouldBe(TaskStatus.RanToCompletion);
    }

    [Fact]
    public async Task RestartingAfterStopRunsANewGeneration()
    {
        // Arrange
        await Target.StartAsync(CancellationToken);
        await Target.WaitForGenerationAsync(CancellationToken);
        await Target.StopAsync(CancellationToken);
        var firstExecuteTask = Target.ExecuteTask;

        // Act
        await Target.StartAsync(CancellationToken);
        await Target.WaitForGenerationAsync(CancellationToken);
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.GenerationCount.ShouldBe(2);
        Target.ExecuteTask.ShouldNotBeSameAs(firstExecuteTask);
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
    }

    [Fact]
    public async Task StartingWhileThePreviousGenerationIsStillRunningWaitsForItInsteadOfOverlapping()
    {
        // Arrange
        Target.IgnoresCancellation = true;
        await Target.StartAsync(CancellationToken);
        await Target.WaitForGenerationAsync(CancellationToken);
        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Target.StopAsync(stopTimeout.Token);
        var firstExecuteTask = Target.ExecuteTask;

        // Act
        var startTask = Target.StartAsync(CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken);
        var stillWaitingForThePreviousGeneration = !startTask.IsCompleted;
        Target.Release();
        await startTask.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);

        // Assert
        stillWaitingForThePreviousGeneration.ShouldBeTrue();
        firstExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
        Target.ExecuteTask.ShouldNotBeSameAs(firstExecuteTask);
    }

    [Fact]
    public async Task OverlappingStartsBeginOnlyOneNewGeneration()
    {
        // Arrange
        Target.IgnoresCancellation = true;
        await Target.StartAsync(CancellationToken);
        await Target.WaitForGenerationAsync(CancellationToken);
        Target.IgnoresCancellation = false;

        // Act
        var firstStart = Target.StartAsync(CancellationToken);
        var secondStart = Target.StartAsync(CancellationToken);
        Target.Release();
        await Task.WhenAll(firstStart, secondStart).WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
        await Target.WaitForGenerationAsync(CancellationToken);
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.GenerationCount.ShouldBe(2);
        Target.MaxConcurrentGenerations.ShouldBe(1);
    }

    [Fact]
    public async Task StoppingAfterDisposeCompletesGracefully()
    {
        // Arrange
        await Target.StartAsync(CancellationToken);
        await Target.WaitForGenerationAsync(CancellationToken);
        Target.Dispose();

        // Act
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
    }

    [Fact]
    public async Task StartingAfterDisposeDoesNothing()
    {
        // Arrange
        Target.Dispose();

        // Act
        await Target.StartAsync(CancellationToken);

        // Assert
        Target.ExecuteTask.ShouldBeNull();
        Target.GenerationCount.ShouldBe(0);
    }

    [Fact]
    public async Task StoppingTwiceCompletesGracefully()
    {
        // Arrange
        await Target.StartAsync(CancellationToken);
        await Target.WaitForGenerationAsync(CancellationToken);

        // Act
        await Target.StopAsync(CancellationToken);
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
    }

    [Fact]
    public async Task DisposingTwiceIsSafe()
    {
        // Arrange
        await Target.StartAsync(CancellationToken);
        await Target.WaitForGenerationAsync(CancellationToken);

        // Act
        Target.Dispose();
        Target.Dispose();

        // Assert
        await Target.ExecuteTask!;
        Target.ExecuteTask.Status.ShouldBe(TaskStatus.RanToCompletion);
    }

    [Fact]
    public async Task StoppingAndRestartingConcurrentlyConvergesCleanly()
    {
        // Arrange
        await Target.StartAsync(CancellationToken);

        // Act
        for (var i = 0; i < 300; i++)
        {
            using var startSignal = new SemaphoreSlim(0, 2);
            var stopTask = Task.Run(
                async () =>
                {
                    await startSignal.WaitAsync(CancellationToken);
                    await Target.StopAsync(CancellationToken);
                },
                CancellationToken);
            var startTask = Task.Run(
                async () =>
                {
                    await startSignal.WaitAsync(CancellationToken);
                    await Target.StartAsync(CancellationToken);
                },
                CancellationToken);
            startSignal.Release(2);
            await Task.WhenAll(stopTask, startTask).WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            await Target.StopAsync(CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);
            await Target.StartAsync(CancellationToken);
        }

        await Target.StopAsync(CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
        Target.MaxConcurrentGenerations.ShouldBe(1);
    }

    [Fact]
    public async Task AFaultedGenerationStopsTheApplication()
    {
        // Arrange
        Target.Faults = true;

        // Act
        await Target.StartAsync(CancellationToken);
        await Target.ExecuteTask!;

        // Assert
        Target.ExecuteTask.Status.ShouldBe(TaskStatus.RanToCompletion);
        GetMock<IHostApplicationLifetime>().Verify(lifetime => lifetime.StopApplication(), Times.Once);
    }

    [Fact]
    public async Task AStoppedGenerationDoesNotStopTheApplication()
    {
        // Arrange
        await Target.StartAsync(CancellationToken);
        await Target.WaitForGenerationAsync(CancellationToken);

        // Act
        await Target.StopAsync(CancellationToken);

        // Assert
        GetMock<IHostApplicationLifetime>().Verify(lifetime => lifetime.StopApplication(), Times.Never);
    }

    /// <summary>
    /// A service whose generations announce themselves and then wait until they are stopped. A generation can instead
    /// ignore its token until the test calls <see cref="Release"/> — like one that has not yet noticed its own
    /// cancellation — or fault. It also records the highest number of generations that ever ran at once.
    /// </summary>
    public sealed class ProbeService(IHostApplicationLifetime applicationLifetime, ILogger<ProbeService> logger)
        : RestartableBackgroundService(applicationLifetime, logger)
    {
        private readonly Channel<bool> _generations = Channel.CreateUnbounded<bool>();
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _generationCount;
        private int _running;
        private int _maxConcurrentGenerations;

        public bool IgnoresCancellation { get; set; }

        public bool Faults { get; set; }

        public int GenerationCount => Volatile.Read(ref _generationCount);

        public int MaxConcurrentGenerations => Volatile.Read(ref _maxConcurrentGenerations);

        public async Task WaitForGenerationAsync(CancellationToken cancellationToken) => await _generations.Reader.ReadAsync(cancellationToken);

        public void Release() => _release.TrySetResult();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Interlocked.Increment(ref _generationCount);
            TrackMax(Interlocked.Increment(ref _running));
            try
            {
                _generations.Writer.TryWrite(true);
                if (Faults)
                {
                    throw new InvalidOperationException("Simulated generation fault.");
                }

                if (IgnoresCancellation)
                {
                    await _release.Task;
                    return;
                }

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            finally
            {
                Interlocked.Decrement(ref _running);
            }
        }

        private void TrackMax(int observed)
        {
            var seen = Volatile.Read(ref _maxConcurrentGenerations);
            while (observed > seen)
            {
                var previous = Interlocked.CompareExchange(ref _maxConcurrentGenerations, observed, seen);
                if (previous == seen)
                {
                    return;
                }

                seen = previous;
            }
        }
    }
}
