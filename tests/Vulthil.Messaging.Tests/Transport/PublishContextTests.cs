using System.Text.Json;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests.Transport;

public sealed class PublishContextTests : BaseUnitTestCase<PublishContext>
{
    private sealed record TestMessage(string Content);

    [Fact]
    public void FaultAddressRoundTripsThroughGetterForQueueUri()
    {
        // Arrange
        var address = new Uri("queue:order-faults");

        // Act
        Target.SetFaultAddress(address);

        // Assert
        Target.FaultAddress.ShouldBe(address);
    }

    [Fact]
    public void ResponseAddressRoundTripsThroughGetterForQueueUri()
    {
        // Arrange
        var address = new Uri("queue:order-replies");

        // Act
        Target.SetResponseAddress(address);

        // Assert
        Target.ResponseAddress.ShouldBe(address);
    }

    [Fact]
    public void DestinationAddressRoundTripsThroughGetterForQueueUri()
    {
        // Arrange
        var address = new Uri("queue:orders");

        // Act
        Target.DestinationAddress = address;

        // Assert
        Target.DestinationAddress.ShouldBe(address);
    }

    [Fact]
    public void SourceAddressRoundTripsThroughGetterForAbsoluteAmqpUri()
    {
        // Arrange
        var address = new Uri("amqp://broker/source");

        // Act
        Target.SourceAddress = address;

        // Assert
        Target.SourceAddress.ShouldBe(address);
    }

    [Fact]
    public void FaultAddressIsNullWhenNotSet()
    {
        // Act & Assert
        Target.FaultAddress.ShouldBeNull();
    }

    [Fact]
    public void CreateEnvelopeWithQueueFaultAddressDoesNotThrowAndCarriesTheAddress()
    {
        // Arrange
        Target.SetFaultAddress(new Uri("queue:order-faults"));

        // Act
        var envelope = MessageEnvelopeFactory.Create(
            new TestMessage("payload"),
            Target,
            messageId: "msg-1",
            correlationId: "corr-1",
            urn: new Uri("urn:message:TestMessage"),
            jsonOptions: JsonSerializerOptions.Default);

        // Assert
        envelope.FaultAddress.ShouldBe("queue:order-faults");
    }
}
