using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class RabbitMqAddressTests : BaseUnitTestCase
{
    [Fact]
    public void ResolveRoutingKeyReturnsNullForNoAddress()
    {
        // Act
        var routingKey = RabbitMqAddress.ResolveRoutingKey(null);

        // Assert
        routingKey.ShouldBeNull();
    }

    [Fact]
    public void ResolveRoutingKeyUsesTheQueueNameOfAQueueAddress()
    {
        // Act
        var routingKey = RabbitMqAddress.ResolveRoutingKey(new Uri("queue:orders"));

        // Assert
        routingKey.ShouldBe("orders");
    }

    [Theory]
    [InlineData("amqp://broker/orders")]
    [InlineData("amqps://broker:5671/orders")]
    [InlineData("rabbitmq://broker/orders")]
    public void ResolveRoutingKeyUsesThePathOfAnAmqpAddress(string address)
    {
        // Act
        var routingKey = RabbitMqAddress.ResolveRoutingKey(new Uri(address));

        // Assert
        routingKey.ShouldBe("orders");
    }

    [Fact]
    public void ResolveRoutingKeyPassesAnyOtherAddressThroughInFull()
    {
        // Act
        var routingKey = RabbitMqAddress.ResolveRoutingKey(new Uri("https://example.test/hooks/orders"));

        // Assert
        routingKey.ShouldBe("https://example.test/hooks/orders");
    }
}
