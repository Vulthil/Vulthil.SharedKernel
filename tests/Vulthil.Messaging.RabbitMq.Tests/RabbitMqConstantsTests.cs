using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class RabbitMqConstantsTests : BaseUnitTestCase
{
    [Theory]
    [InlineData(5000d, "5000")]
    [InlineData(1500.5d, "1501")]
    [InlineData(1123.4567d, "1124")]
    [InlineData(0.25d, "1")]
    [InlineData(0d, "0")]
    [InlineData(-5d, "0")]
    public void AnExpirationIsWrittenAsWholeMillisecondsRoundedUp(double milliseconds, string expected)
    {
        // Act
        var expiration = RabbitMqConstants.FormatExpiration(TimeSpan.FromMilliseconds(milliseconds));

        // Assert
        expiration.ShouldBe(expected);
    }

    [Fact]
    public void AWrittenExpirationReadsBackAsTheInstantItWasRoundedUpTo()
    {
        // Arrange
        var sentTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var expiration = RabbitMqConstants.FormatExpiration(TimeSpan.FromMilliseconds(1500.5));

        // Act
        var expiresAt = RabbitMqConstants.TryParseExpiration(expiration, sentTime);

        // Assert
        expiresAt.ShouldBe(sentTime.AddMilliseconds(1501));
    }
}
