using Vulthil.xUnit;

namespace Vulthil.SharedKernel.Outbox.Tests;

public sealed class OutboxRelayBackoffTests : BaseUnitTestCase
{
    private const int BatchSize = 10;

    private static readonly OutboxProcessingOptions RelayOptions = new()
    {
        BatchSize = BatchSize,
        OutboxProcessingDelaySeconds = 2,
        MaxDelaySeconds = 60,
    };

    [Fact]
    public void AFullyRelayedFullBatchRunsTheNextCycleAtOnce()
    {
        // Act
        var delay = OutboxRelayBackoff.After(new OutboxRelayCycleResult(Claimed: BatchSize, Relayed: BatchSize), TimeSpan.FromSeconds(8), RelayOptions);

        // Assert
        delay.ShouldBe(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(4, 4)]
    [InlineData(BatchSize, 7)]
    public void ACycleThatRelayedLessThanAFullBatchWaitsTheBaseDelay(int claimed, int relayed)
    {
        // Act
        var delay = OutboxRelayBackoff.After(new OutboxRelayCycleResult(claimed, relayed), TimeSpan.FromSeconds(8), RelayOptions);

        // Assert
        delay.ShouldBe(TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(8, 16)]
    [InlineData(40, 60)]
    [InlineData(60, 60)]
    public void ACycleThatRelayedNothingDoublesThePreviousDelayWithinTheBaseAndMaximumDelays(int previousSeconds, int expectedSeconds)
    {
        // Act
        var delay = OutboxRelayBackoff.After(new OutboxRelayCycleResult(Claimed: 0, Relayed: 0), TimeSpan.FromSeconds(previousSeconds), RelayOptions);

        // Assert
        delay.ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void ACycleWhoseEveryClaimedMessageFailedBacksOffLikeACycleThatFoundNothing()
    {
        // Act
        var delay = OutboxRelayBackoff.After(new OutboxRelayCycleResult(Claimed: BatchSize, Relayed: 0), TimeSpan.FromSeconds(8), RelayOptions);

        // Assert
        delay.ShouldBe(TimeSpan.FromSeconds(16));
    }

    [Fact]
    public void AFaultedCycleWaitsTheBaseDelay()
    {
        // Act
        var delay = OutboxRelayBackoff.AfterFault(RelayOptions);

        // Assert
        delay.ShouldBe(TimeSpan.FromSeconds(2));
    }
}
