using Vulthil.Messaging.Queues;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests;

public sealed class RetryPolicyConfiguratorTests : BaseUnitTestCase<RetryPolicyConfigurator>
{
    protected override RetryPolicyConfigurator CreateInstance() => new();

    [Fact]
    public void UseJitterThrowsWhenFactorIsAboveOne()
    {
        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => Target.UseJitter(1.5));
    }

    [Fact]
    public void UseJitterThrowsWhenFactorIsBelowZero()
    {
        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => Target.UseJitter(-0.1));
    }

    [Fact]
    public void ImmediateConfiguresZeroDelayIntervals()
    {
        // Act
        Target.Immediate(3);

        // Assert
        Target.RetryLimit.ShouldBe(3);
        Target.Intervals.Count.ShouldBe(3);
        Target.Intervals.ShouldAllBe(interval => interval == TimeSpan.Zero);
    }

    [Fact]
    public void RetryDefaultsToDelayedRedelivery()
    {
        // Act
        Target.Immediate(2);

        // Assert
        Target.Build().InMemory.ShouldBeFalse();
    }

    [Fact]
    public void InMemoryEnablesInMemoryRetry()
    {
        // Act
        Target.Immediate(2);
        Target.InMemory();

        // Assert
        Target.Build().InMemory.ShouldBeTrue();
    }

    [Fact]
    public void TheSettingsChainOnTheSameConfigurator()
    {
        // Act
        var chained = Target
            .Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(4))
            .UseJitter(0.2)
            .Ignore<TimeoutException>()
            .InMemory();

        // Assert
        chained.ShouldBeSameAs(Target);
        var policy = Target.Build();
        policy.MaxRetryCount.ShouldBe(3);
        policy.JitterFactor.ShouldBe(0.2);
        policy.InMemory.ShouldBeTrue();
        policy.GetIgnoredExceptionTypes().ShouldBe([typeof(TimeoutException)]);
    }
}
