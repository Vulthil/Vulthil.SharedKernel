using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Outbox.Tests;

public sealed class OutboxRetentionBackgroundServiceTests : BaseUnitTestCase
{
    private readonly Lazy<OutboxRetentionBackgroundService> _lazyTarget;

    private OutboxRetentionBackgroundService Target => _lazyTarget.Value;

    public OutboxRetentionBackgroundServiceTests()
    {
        _lazyTarget = new(CreateInstance<OutboxRetentionBackgroundService>);
        Use(TimeProvider.System);
    }

    [Fact]
    public async Task StoreWithoutRetentionSupportLogsExactlyOneWarningAcrossMultipleSweeps()
    {
        // Arrange
        Use<IOptions<OutboxProcessingOptions>>(Options.Create(new OutboxProcessingOptions
        {
            Retention = { SweepInterval = TimeSpan.FromMilliseconds(5) }
        }));
        Use<IServiceScopeFactory>(new AutoMockerServiceScopeFactory(AutoMocker));
        var logger = new WarningCountingLogger();
        Use<ILogger<OutboxRetentionBackgroundService>>(logger);

        // Act
        await Target.StartAsync(CancellationToken);
        await Task.WhenAny(logger.FirstWarning, Task.Delay(TimeSpan.FromSeconds(5), CancellationToken));
        await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken);
        await Target.StopAsync(CancellationToken);

        // Assert
        logger.WarningCount.ShouldBe(1);
    }

    [Fact]
    public async Task AForeignCancellationDuringASweepDoesNotEndTheExecuteTask()
    {
        // Arrange
        Use<IOptions<OutboxProcessingOptions>>(Options.Create(new OutboxProcessingOptions()));
        Use<IServiceScopeFactory>(new AutoMockerServiceScopeFactory(AutoMocker));
        var store = new TimeoutThrowingRetentionStore();
        Use<IOutboxStore>(store);

        // Act
        await Target.StartAsync(CancellationToken);
        await store.FirstSweepAttempted;
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
    }

    private sealed class TimeoutThrowingRetentionStore : IOutboxStore, IOutboxRetentionStore
    {
        private readonly TaskCompletionSource _firstSweepAttempted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task FirstSweepAttempted => _firstSweepAttempted.Task;

        public bool IsInTransaction => false;

        public void AddOutboxMessage(OutboxMessage message)
        {
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<int> ProcessBatchAsync(Func<OutboxMessageData, CancellationToken, Task<string?>> dispatch, CancellationToken cancellationToken) => Task.FromResult(0);

        public Task<int> DeleteProcessedAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken)
        {
            _firstSweepAttempted.TrySetResult();
            throw new OperationCanceledException();
        }
    }

    private sealed class WarningCountingLogger : ILogger<OutboxRetentionBackgroundService>
    {
        private readonly TaskCompletionSource _firstWarning = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int WarningCount { get; private set; }

        public Task FirstWarning => _firstWarning.Task;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel != LogLevel.Warning)
            {
                return;
            }

            WarningCount++;
            _firstWarning.TrySetResult();
        }
    }

    private sealed class AutoMockerServiceScopeFactory(IServiceProvider serviceProvider) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new PassthroughServiceScope(serviceProvider);

        private sealed class PassthroughServiceScope(IServiceProvider serviceProvider) : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = serviceProvider;

            public void Dispose()
            {
            }
        }
    }
}
