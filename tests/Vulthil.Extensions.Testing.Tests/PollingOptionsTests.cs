using Vulthil.xUnit;

namespace Vulthil.Extensions.Testing.Tests;

public sealed class PollingOptionsTests : BaseUnitTestCase
{
    [Fact]
    public void DefaultsToAOneSecondTickOnTheSystemClockWithoutAFailedAttemptFilter()
    {
        // Act
        var options = new PollingOptions(TimeSpan.FromSeconds(5));

        // Assert
        options.Timeout.ShouldBe(TimeSpan.FromSeconds(5));
        options.TimerTick.ShouldBe(TimeSpan.FromSeconds(1));
        options.TimeProvider.ShouldBeSameAs(TimeProvider.System);
        options.TreatAsFailedAttempt.ShouldBeNull();
    }
}
