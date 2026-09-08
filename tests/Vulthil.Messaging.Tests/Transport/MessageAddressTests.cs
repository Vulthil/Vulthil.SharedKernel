using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests.Transport;

public sealed class MessageAddressTests : BaseUnitTestCase
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseTreatsANullOrBlankValueAsNoAddress(string? value)
    {
        // Act
        var address = MessageAddress.Parse(value);

        // Assert
        address.ShouldBeNull();
    }

    [Fact]
    public void ParseKeepsAnAbsoluteUri()
    {
        // Act
        var address = MessageAddress.Parse("amqp://broker/orders");

        // Assert
        address.ShouldBe(new Uri("amqp://broker/orders"));
    }

    [Fact]
    public void ParseTreatsABareNameAsAQueue()
    {
        // Act
        var address = MessageAddress.Parse("orders");

        // Assert
        address.ShouldBe(new Uri("queue:orders"));
    }

    [Fact]
    public void QueueBuildsTheQueueAddressForAName()
    {
        // Act
        var address = MessageAddress.Queue("order-replies");

        // Assert
        address.ShouldBe(new Uri("queue:order-replies"));
        address.Scheme.ShouldBe("queue");
    }

    [Theory]
    [InlineData("queue:orders")]
    [InlineData("queue:/orders")]
    public void QueueNameReturnsTheNameOfAQueueAddress(string address)
    {
        // Act
        var queueName = MessageAddress.QueueName(new Uri(address));

        // Assert
        queueName.ShouldBe("orders");
    }

    [Fact]
    public void QueueNameIsNullForAnyOtherScheme()
    {
        // Act
        var queueName = MessageAddress.QueueName(new Uri("amqp://broker/orders"));

        // Assert
        queueName.ShouldBeNull();
    }

    [Fact]
    public void ToHeaderValueStoresAQueueAddressAsItsBareName()
    {
        // Act
        var value = MessageAddress.ToHeaderValue(new Uri("queue:orders"));

        // Assert
        value.ShouldBe("orders");
    }

    [Fact]
    public void ToHeaderValueStoresAnyOtherAddressInFull()
    {
        // Act
        var value = MessageAddress.ToHeaderValue(new Uri("https://example.test/hooks/orders"));

        // Assert
        value.ShouldBe("https://example.test/hooks/orders");
    }

    [Theory]
    [InlineData("queue:orders")]
    [InlineData("amqp://broker/orders")]
    [InlineData("https://example.test/hooks/orders")]
    public void ParseReversesToHeaderValue(string address)
    {
        // Arrange
        var original = new Uri(address);

        // Act
        var roundTripped = MessageAddress.Parse(MessageAddress.ToHeaderValue(original));

        // Assert
        roundTripped.ShouldBe(original);
    }

    [Fact]
    public void QueueNameAndToHeaderValueRejectANullAddress()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => MessageAddress.QueueName(null!));
        Should.Throw<ArgumentNullException>(() => MessageAddress.ToHeaderValue(null!));
        Should.Throw<ArgumentNullException>(() => MessageAddress.Queue(null!));
    }
}
