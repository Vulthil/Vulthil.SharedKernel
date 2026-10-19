using Microsoft.Extensions.Options;
using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Outbox.Tests;

public sealed class OutboxRetentionSweepTargetTests : BaseUnitTestCase
{
    private const int BatchSize = 100;
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SweepCutoff = Now - TimeSpan.FromDays(7);

    private readonly OutboxProcessingOptions _options = new();
    private readonly Lazy<OutboxRetentionSweepTarget> _lazyTarget;

    private OutboxRetentionSweepTarget Target => _lazyTarget.Value;

    public OutboxRetentionSweepTargetTests()
    {
        _lazyTarget = new(CreateInstance<OutboxRetentionSweepTarget>);
        Use<IOptions<OutboxProcessingOptions>>(Options.Create(_options));
        Use<TimeProvider>(new FakeTimeProvider(Now));
    }

    [Fact]
    public void AStoreThatCannotDeleteOldRowsGetsNoDeleter()
    {
        // Arrange
        Use<IOutboxStore>(Mock.Of<IOutboxStore>());

        // Act
        var deleter = Target.CreateDeleter();

        // Assert
        deleter.ShouldBeNull();
    }

    [Fact]
    public async Task WithoutADeadLetterRetentionPeriodDeadLetteredRowsUseTheSweepCutoff()
    {
        // Arrange
        var retentionStore = SetupRetentionStore(deletedRows: 3);

        // Act
        var deleter = Target.CreateDeleter().ShouldNotBeNull();
        var deleted = await deleter(SweepCutoff, BatchSize, CancellationToken);

        // Assert
        deleted.ShouldBe(3);
        retentionStore.Verify(store => store.DeleteProcessedAsync(SweepCutoff, SweepCutoff, BatchSize, CancellationToken), Times.Once);
    }

    [Fact]
    public async Task ADeadLetterRetentionPeriodSetsTheDeadLetteredCutoffThatLongBeforeNow()
    {
        // Arrange
        _options.Retention.DeadLetterRetentionPeriod = TimeSpan.FromDays(30);
        var retentionStore = SetupRetentionStore(deletedRows: 0);

        // Act
        var deleter = Target.CreateDeleter().ShouldNotBeNull();
        await deleter(SweepCutoff, BatchSize, CancellationToken);

        // Assert
        retentionStore.Verify(store => store.DeleteProcessedAsync(SweepCutoff, Now - TimeSpan.FromDays(30), BatchSize, CancellationToken), Times.Once);
    }

    [Fact]
    public async Task ADeadLetterRetentionPeriodReachingBackPastTheEarliestTimeKeepsEveryDeadLetteredRow()
    {
        // Arrange
        _options.Retention.DeadLetterRetentionPeriod = TimeSpan.MaxValue;
        var retentionStore = SetupRetentionStore(deletedRows: 0);

        // Act
        var deleter = Target.CreateDeleter().ShouldNotBeNull();
        await deleter(SweepCutoff, BatchSize, CancellationToken);

        // Assert
        retentionStore.Verify(store => store.DeleteProcessedAsync(SweepCutoff, DateTimeOffset.MinValue, BatchSize, CancellationToken), Times.Once);
    }

    private Mock<IOutboxRetentionStore> SetupRetentionStore(int deletedRows)
    {
        var retentionStore = GetMock<IOutboxStore>().As<IOutboxRetentionStore>();
        retentionStore
            .Setup(store => store.DeleteProcessedAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedRows);
        return retentionStore;
    }
}
