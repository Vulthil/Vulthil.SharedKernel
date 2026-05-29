using System.Text.Json;
using Vulthil.Messaging.RabbitMq.Envelope;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class MessageEnvelopeTests : BaseUnitTestCase
{
    private static JsonSerializerOptions Options => new() { WriteIndented = false };

    [Fact]
    public void EnvelopeRoundtripsThroughJson()
    {
        // Arrange
        var original = new MessageEnvelope
        {
            MessageId = "msg-1",
            RequestId = "req-1",
            CorrelationId = "corr-1",
            ConversationId = "conv-1",
            InitiatorId = "init-1",
            SourceAddress = "queue:producer",
            DestinationAddress = "queue:fulfillment",
            ResponseAddress = "queue:reply",
            FaultAddress = "queue:faults",
            MessageType = new Uri("urn:message:Acme.Orders:OrderPlaced"),
            Message = JsonSerializer.SerializeToElement(new { orderId = "abc", amount = 42 }),
            SentTime = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000),
            ExpirationTime = DateTimeOffset.FromUnixTimeSeconds(1_700_000_300),
            Headers = new Dictionary<string, object?> { ["tenant"] = "acme" },
        };

        // Act
        var json = JsonSerializer.SerializeToUtf8Bytes(original, Options);
        var roundtripped = JsonSerializer.Deserialize<MessageEnvelope>(json, Options);

        // Assert
        roundtripped.ShouldNotBeNull();
        roundtripped.MessageId.ShouldBe("msg-1");
        roundtripped.RequestId.ShouldBe("req-1");
        roundtripped.CorrelationId.ShouldBe("corr-1");
        roundtripped.ConversationId.ShouldBe("conv-1");
        roundtripped.InitiatorId.ShouldBe("init-1");
        roundtripped.SourceAddress.ShouldBe("queue:producer");
        roundtripped.DestinationAddress.ShouldBe("queue:fulfillment");
        roundtripped.ResponseAddress.ShouldBe("queue:reply");
        roundtripped.FaultAddress.ShouldBe("queue:faults");
        roundtripped.MessageType.AbsoluteUri.ShouldBe("urn:message:Acme.Orders:OrderPlaced");
        roundtripped.Message.GetProperty("orderId").GetString().ShouldBe("abc");
        roundtripped.Message.GetProperty("amount").GetInt32().ShouldBe(42);
        roundtripped.SentTime.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));
        roundtripped.ExpirationTime.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1_700_000_300));
        roundtripped.Headers.ShouldNotBeNull();
        roundtripped.Headers["tenant"]!.ToString().ShouldBe("acme");
    }

    [Fact]
    public void EnvelopeSerializesWithLowerCamelCasePropertyNames()
    {
        // Arrange
        var envelope = new MessageEnvelope
        {
            MessageType = new Uri("urn:message:T"),
            Message = JsonSerializer.SerializeToElement(new { x = 1 }),
        };

        // Act
        var json = JsonSerializer.Serialize(envelope, Options);

        // Assert
        json.ShouldContain("\"messageType\"", Case.Sensitive);
        json.ShouldContain("\"message\"", Case.Sensitive);
    }
}
