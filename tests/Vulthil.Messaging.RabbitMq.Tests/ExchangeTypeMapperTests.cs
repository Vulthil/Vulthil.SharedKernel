using Vulthil.Messaging.RabbitMq;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

/// <summary>
/// Represents the ExchangeTypeMapperTests.
/// </summary>
public sealed class ExchangeTypeMapperTests : BaseUnitTestCase
{
    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void TopicExchangeShouldMapToTopicType()
    {
        // Arrange & Act
        var result = MessagingExchangeType.Topic.ToRabbitExchangeType();

        // Assert
        result.ShouldBe("topic");
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void DirectExchangeShouldMapToDirectType()
    {
        // Arrange & Act
        var result = MessagingExchangeType.Direct.ToRabbitExchangeType();

        // Assert
        result.ShouldBe("direct");
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void FanoutExchangeShouldMapToFanoutType()
    {
        // Arrange & Act
        var result = MessagingExchangeType.Fanout.ToRabbitExchangeType();

        // Assert
        result.ShouldBe("fanout");
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void HeadersExchangeShouldMapToHeadersType()
    {
        // Arrange & Act
        var result = MessagingExchangeType.Headers.ToRabbitExchangeType();

        // Assert
        result.ShouldBe("headers");
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
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
