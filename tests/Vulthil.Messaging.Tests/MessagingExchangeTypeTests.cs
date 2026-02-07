using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests;

public sealed class MessagingExchangeTypeTests : BaseUnitTestCase
{
    [Fact]
    public void FanoutExchangeTypeShouldExist()
    {
        // Arrange & Act & Assert
        MessagingExchangeType.Fanout.ShouldBe(MessagingExchangeType.Fanout);
    }

    [Fact]
    public void DirectExchangeTypeShouldExist()
    {
        // Arrange & Act & Assert
        MessagingExchangeType.Direct.ShouldBe(MessagingExchangeType.Direct);
    }

    [Fact]
    public void TopicExchangeTypeShouldExist()
    {
        // Arrange & Act & Assert
        MessagingExchangeType.Topic.ShouldBe(MessagingExchangeType.Topic);
    }

    [Fact]
    public void HeadersExchangeTypeShouldExist()
    {
        // Arrange & Act & Assert
        MessagingExchangeType.Headers.ShouldBe(MessagingExchangeType.Headers);
    }

    [Fact]
    public void AllExchangeTypesAreUnique()
    {
        // Arrange & Act
        var types = new[]
        {
            MessagingExchangeType.Fanout,
            MessagingExchangeType.Direct,
            MessagingExchangeType.Topic,
            MessagingExchangeType.Headers
        };

        // Assert
        types.Length.ShouldBe(4);
        types.Distinct().Count().ShouldBe(4);
    }
}
