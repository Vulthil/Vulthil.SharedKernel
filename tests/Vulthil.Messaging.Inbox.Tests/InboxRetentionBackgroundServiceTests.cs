using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Inbox.Tests;

public sealed class InboxRetentionBackgroundServiceTests : BaseUnitTestCase
{
    private readonly Lazy<InboxRetentionBackgroundService> _lazyTarget;

    private InboxRetentionBackgroundService Target => _lazyTarget.Value;

    public InboxRetentionBackgroundServiceTests()
    {
        _lazyTarget = new(CreateInstance<InboxRetentionBackgroundService>);
        Use(TimeProvider.System);
    }

    [Fact]
    public async Task StoreWithoutRetentionSupportLogsExactlyOneWarningAcrossMultipleSweeps()
    {
        // Arrange
        Use<IOptions<InboxOptions>>(Options.Create(new InboxOptions
        {
            Retention = { SweepInterval = TimeSpan.FromMilliseconds(5) }
        }));
        Use<IServiceScopeFactory>(new AutoMockerServiceScopeFactory(AutoMocker));
        var logger = new WarningCountingLogger();
        Use<ILogger<InboxRetentionBackgroundService>>(logger);

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
        Use<IOptions<InboxOptions>>(Options.Create(new InboxOptions()));
        Use<IServiceScopeFactory>(new AutoMockerServiceScopeFactory(AutoMocker));
        var store = new TimeoutThrowingRetentionStore();
        Use<IIdempotencyStore>(store);

        // Act
        await Target.StartAsync(CancellationToken);
        await store.FirstSweepAttempted;
        await Target.StopAsync(CancellationToken);

        // Assert
        Target.ExecuteTask!.Status.ShouldBe(TaskStatus.RanToCompletion);
    }

    private sealed class TimeoutThrowingRetentionStore : IIdempotencyStore, IInboxRetentionStore
    {
        private readonly TaskCompletionSource _firstSweepAttempted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task FirstSweepAttempted => _firstSweepAttempted.Task;

        public Task<bool> ProcessAsync(string idempotencyKey, IMessageContext context, Func<CancellationToken, Task> process, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<int> DeleteProcessedAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken)
        {
            _firstSweepAttempted.TrySetResult();
            throw new OperationCanceledException();
        }
    }

    private sealed class WarningCountingLogger : ILogger<InboxRetentionBackgroundService>
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
