using System.Text.Json;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests.Transport;

public sealed class MessageHeadersTests : BaseUnitTestCase
{
    [Theory]
    [InlineData(MessageHeaders.ConversationId)]
    [InlineData(MessageHeaders.InitiatorId)]
    [InlineData(MessageHeaders.SourceAddress)]
    [InlineData(MessageHeaders.DestinationAddress)]
    [InlineData(MessageHeaders.ResponseAddress)]
    [InlineData(MessageHeaders.FaultAddress)]
    public void IsReservedRecognisesEveryReservedKey(string key)
    {
        // Act & Assert
        MessageHeaders.IsReserved(key).ShouldBeTrue();
    }

    [Theory]
    [InlineData("x-tenant")]
    [InlineData("responseaddress")]
    [InlineData("MessageId")]
    public void IsReservedRejectsCustomAndDifferentlyCasedKeys(string key)
    {
        // Act & Assert
        MessageHeaders.IsReserved(key).ShouldBeFalse();
    }

    [Fact]
    public void IsReservedRejectsANullKey()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => MessageHeaders.IsReserved(null!));
    }

    [Fact]
    public void EveryReservedKeySetThroughThePublishContextIsPromotedOutOfTheEnvelopeHeaders()
    {
        // Arrange
        var publishContext = new PublishContext
        {
            SourceAddress = new Uri("queue:orders-api"),
            DestinationAddress = new Uri("queue:orders"),
        };
        publishContext.SetConversationId("conv-1");
        publishContext.SetInitiatorId("msg-0");
        publishContext.SetResponseAddress(new Uri("queue:order-replies"));
        publishContext.SetFaultAddress(new Uri("amqp://broker/order-faults"));
        publishContext.AddHeader("x-tenant", "acme");

        // Act
        var envelope = MessageEnvelopeFactory.Create(
            new TestMessage("payload"),
            publishContext,
            messageId: "msg-1",
            correlationId: "corr-1",
            urn: new Uri("urn:message:Tests:TestMessage"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        // Assert
        envelope.Headers.ShouldNotBeNull().Keys.ShouldBe(["x-tenant"]);
        envelope.ConversationId.ShouldBe("conv-1");
        envelope.InitiatorId.ShouldBe("msg-0");
        envelope.SourceAddress.ShouldBe("queue:orders-api");
        envelope.DestinationAddress.ShouldBe("queue:orders");
        envelope.ResponseAddress.ShouldBe("queue:order-replies");
        envelope.FaultAddress.ShouldBe("amqp://broker/order-faults");
    }

    private sealed record TestMessage(string Content);
}
