using System.Text;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class RabbitMqConstantsTests : BaseUnitTestCase
{
    [Theory]
    [InlineData("2", 2)]
    [InlineData("1000", 1000)]
    [InlineData(" 3 ", 3)]
    [InlineData("0", 0)]
    [InlineData("-1", 0)]
    [InlineData("+1", 0)]
    [InlineData("1.5", 0)]
    [InlineData("99999999999", 0)]
    [InlineData("not a number", 0)]
    [InlineData("", 0)]
    public void ARetryCountSentAsTextCountsOnlyWhenItIsAWholeNumber(string text, int expected)
    {
        // Arrange
        var headers = new Dictionary<string, object?> { [RabbitMqConstants.RetryCountHeader] = Encoding.UTF8.GetBytes(text) };

        // Act
        var retryCount = RabbitMqConstants.GetRetryCount(headers);

        // Assert
        retryCount.ShouldBe(expected);
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(3L, 3)]
    [InlineData((short)3, 3)]
    [InlineData((byte)3, 3)]
    [InlineData((long)int.MaxValue, int.MaxValue)]
    [InlineData(-1, 0)]
    [InlineData(-1L, 0)]
    [InlineData(5_000_000_000L, 0)]
    [InlineData(true, 0)]
    [InlineData(null, 0)]
    public void ARetryCountSentAsAValueCountsOnlyWhenItIsAWholeNumberThatFitsAnInt(object? value, int expected)
    {
        // Arrange
        var headers = new Dictionary<string, object?> { [RabbitMqConstants.RetryCountHeader] = value };

        // Act
        var retryCount = RabbitMqConstants.GetRetryCount(headers);

        // Assert
        retryCount.ShouldBe(expected);
    }

    [Fact]
    public void ADeliveryWithoutARetryCountStartsAtRoundZero()
    {
        // Act
        var withoutHeaders = RabbitMqConstants.GetRetryCount(null);
        var withoutTheHeader = RabbitMqConstants.GetRetryCount(new Dictionary<string, object?> { ["tenant"] = "acme" });

        // Assert
        withoutHeaders.ShouldBe(0);
        withoutTheHeader.ShouldBe(0);
    }

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
