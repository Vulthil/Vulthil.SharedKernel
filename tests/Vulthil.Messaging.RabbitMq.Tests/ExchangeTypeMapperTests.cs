using Vulthil.Messaging.RabbitMq;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class ExchangeTypeMapperTests : BaseUnitTestCase
{
    [Fact]
    public void TopicExchangeShouldMapToTopicType()
    {
        // Arrange & Act
        var result = MessagingExchangeType.Topic.ToRabbitExchangeType();

        // Assert
        result.ShouldBe("topic");
    }

    [Fact]
    public void DirectExchangeShouldMapToDirectType()
    {
        // Arrange & Act
        var result = MessagingExchangeType.Direct.ToRabbitExchangeType();

        // Assert
        result.ShouldBe("direct");
    }

    [Fact]
    public void FanoutExchangeShouldMapToFanoutType()
    {
        // Arrange & Act
        var result = MessagingExchangeType.Fanout.ToRabbitExchangeType();

        // Assert
        result.ShouldBe("fanout");
    }

    [Fact]
    public void HeadersExchangeShouldMapToHeadersType()
    {
        // Arrange & Act
        var result = MessagingExchangeType.Headers.ToRabbitExchangeType();

        // Assert
        result.ShouldBe("headers");
    }

    [Fact]
    public void InvalidExchangeShouldDefaultToTopic()
    {
        // Arrange & Act
        var result = (MessagingExchangeType)99; // Invalid enum value
        var mappedResult = result.ToRabbitExchangeType();

        // Assert
        mappedResult.ShouldBe("topic");
    }
}
