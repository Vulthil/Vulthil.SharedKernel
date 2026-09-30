using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Outbox.Tests;

/// <summary>
/// Pins the single-slot signal without timing: a semaphore wait that can be satisfied completes synchronously, so
/// whether a wake is pending shows in the returned task's completion state.
/// </summary>
public sealed class OutboxSignalTests : BaseUnitTestCase
{
    private readonly Lazy<OutboxSignal> _lazyTarget;

    private OutboxSignal Target => _lazyTarget.Value;

    public OutboxSignalTests() => _lazyTarget = new(CreateInstance<OutboxSignal>);

    protected override async ValueTask Dispose()
    {
        if (_lazyTarget.IsValueCreated)
        {
            Target.Dispose();
        }

        await base.Dispose();
    }

    [Fact]
    public async Task ANotifyEndsTheNextWaitAtOnce()
    {
        // Arrange
        Target.Notify();

        // Act
        var wait = Target.WaitAsync(Timeout.InfiniteTimeSpan, CancellationToken);

        // Assert
        wait.IsCompleted.ShouldBeTrue();
        await wait;
    }

    [Fact]
    public async Task NotifiesBeforeAWaitCoalesceIntoOneWake()
    {
        // Arrange
        Target.Notify();
        Target.Notify();
        Target.Notify();
        await Target.WaitAsync(Timeout.InfiniteTimeSpan, CancellationToken);

        // Act
        var secondWait = Target.WaitAsync(Timeout.InfiniteTimeSpan, CancellationToken);
        var secondWaitPending = !secondWait.IsCompleted;
        Target.Notify();
        await secondWait;

        // Assert
        secondWaitPending.ShouldBeTrue();
    }

    [Fact]
    public async Task AWaitWithoutANotifyStaysPendingUntilTheNextNotify()
    {
        // Arrange
        var wait = Target.WaitAsync(Timeout.InfiniteTimeSpan, CancellationToken);
        var pendingBeforeNotify = !wait.IsCompleted;

        // Act
        Target.Notify();
        await wait;

        // Assert
        pendingBeforeNotify.ShouldBeTrue();
        wait.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task AWaitEndsAtItsTimeoutWithoutANotify()
    {
        // Act
        var wait = Target.WaitAsync(TimeSpan.Zero, CancellationToken);

        // Assert
        wait.IsCompleted.ShouldBeTrue();
        await wait;
    }

    [Fact]
    public void ANotifyAfterTheSignalIsDisposedDoesNotThrow()
    {
        // Arrange
        Target.Dispose();

        // Act
        var notify = () => Target.Notify();

        // Assert
        notify.ShouldNotThrow();
    }
}
