using System.Text.Json;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests.Transport;

public sealed class MessageContextTests : BaseUnitTestCase
{
    private sealed record TestMessage(string Content);

    private static MessageEnvelope Envelope(
        string? requestId = "req-1",
        string? responseAddress = "queue:reply") => new()
        {
            MessageId = "msg-1",
            RequestId = requestId,
            CorrelationId = "corr-1",
            ConversationId = "conv-1",
            InitiatorId = "init-1",
            SourceAddress = "queue:source",
            DestinationAddress = "amqp://broker/destination",
            ResponseAddress = responseAddress,
            FaultAddress = "queue:faults",
            MessageType = new Uri("urn:message:TestMessage"),
            Message = JsonSerializer.SerializeToElement(new TestMessage("payload")),
            SentTime = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000),
            ExpirationTime = DateTimeOffset.FromUnixTimeSeconds(1_700_000_300),
            Headers = new Dictionary<string, object?> { ["tenant"] = "acme" },
        };

    [Fact]
    public void CreateFromEnvelopeMapsEnvelopeAndTransportFields()
    {
        // Arrange
        var message = new TestMessage("payload");

        // Act
        var context = MessageContext.CreateFromEnvelope(
            message,
            Envelope(),
            routingKey: "orders.created",
            redelivered: true,
            retryCount: 2,
            replyToFallback: "ignored-because-envelope-has-one",
            publisher: null,
            sendEndpointProvider: null,
            CancellationToken.None);

        // Assert
        context.Message.ShouldBe(message);
        context.MessageId.ShouldBe("msg-1");
        context.RequestId.ShouldBe("req-1");
        context.CorrelationId.ShouldBe("corr-1");
        context.ConversationId.ShouldBe("conv-1");
        context.InitiatorId.ShouldBe("init-1");
        context.RoutingKey.ShouldBe("orders.created");
        context.Redelivered.ShouldBeTrue();
        context.RetryCount.ShouldBe(2);
        context.SourceAddress.ShouldBe(new Uri("queue:source"));
        context.DestinationAddress.ShouldBe(new Uri("amqp://broker/destination"));
        context.ResponseAddress.ShouldBe(new Uri("queue:reply"));
        context.FaultAddress.ShouldBe(new Uri("queue:faults"));
        context.SentTime.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));
        context.ExpirationTime.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1_700_000_300));
        context.Headers["tenant"]!.ToString().ShouldBe("acme");
    }

    [Fact]
    public void CreateFromEnvelopeNormalizesWireDeserializedHeaderValuesToClrPrimitives()
    {
        // Arrange — round-trip the envelope through JSON exactly as a broker delivery does.
        var envelope = Envelope() with
        {
            Headers = new Dictionary<string, object?>
            {
                ["tenant"] = "acme",
                ["attempt"] = 3,
                ["big"] = 5_000_000_000L,
                ["ratio"] = 1.5,
                ["critical"] = true,
                ["id"] = Guid.Parse("11111111-2222-3333-4444-555555555555"),
                ["shape"] = new Dictionary<string, int> { ["a"] = 1 },
                ["missing"] = null,
            },
        };
        var wireEnvelope = JsonSerializer.Deserialize<MessageEnvelope>(JsonSerializer.Serialize(envelope))!;

        // Act
        var context = MessageContext.CreateFromEnvelope(
            new TestMessage("payload"),
            wireEnvelope,
            routingKey: "rk",
            redelivered: false,
            retryCount: 0,
            replyToFallback: null,
            publisher: null,
            sendEndpointProvider: null,
            CancellationToken.None);

        // Assert
        context.Headers["tenant"].ShouldBeOfType<string>().ShouldBe("acme");
        context.Headers["attempt"].ShouldBeOfType<int>().ShouldBe(3);
        context.Headers["big"].ShouldBeOfType<long>().ShouldBe(5_000_000_000L);
        context.Headers["ratio"].ShouldBeOfType<double>().ShouldBe(1.5);
        context.Headers["critical"].ShouldBeOfType<bool>().ShouldBe(true);
        context.Headers["id"].ShouldBeOfType<string>().ShouldBe("11111111-2222-3333-4444-555555555555");
        context.Headers["shape"].ShouldBeOfType<JsonElement>().GetProperty("a").GetInt32().ShouldBe(1);
        context.Headers["missing"].ShouldBeNull();
    }

    [Fact]
    public void CreateFromEnvelopeLeavesInMemoryClrHeaderValuesUntouched()
    {
        // Arrange — an in-memory transport hands over the envelope without a JSON round-trip.
        var tenantId = Guid.NewGuid();
        var envelope = Envelope() with
        {
            Headers = new Dictionary<string, object?>
            {
                ["tenant"] = "acme",
                ["attempt"] = 3,
                ["id"] = tenantId,
            },
        };

        // Act
        var context = MessageContext.CreateFromEnvelope(
            new TestMessage("payload"),
            envelope,
            routingKey: "rk",
            redelivered: false,
            retryCount: 0,
            replyToFallback: null,
            publisher: null,
            sendEndpointProvider: null,
            CancellationToken.None);

        // Assert
        context.Headers["tenant"].ShouldBeOfType<string>().ShouldBe("acme");
        context.Headers["attempt"].ShouldBeOfType<int>().ShouldBe(3);
        context.Headers["id"].ShouldBeOfType<Guid>().ShouldBe(tenantId);
    }

    [Fact]
    public void CreateFromEnvelopeFallsBackToReplyToWhenEnvelopeHasNoResponseAddress()
    {
        // Act
        var context = MessageContext.CreateFromEnvelope(
            new TestMessage("payload"),
            Envelope(responseAddress: null),
            routingKey: "rk",
            redelivered: false,
            retryCount: 0,
            replyToFallback: "reply-queue",
            publisher: null,
            sendEndpointProvider: null,
            CancellationToken.None);

        // Assert
        context.ResponseAddress.ShouldBe(new Uri("queue:reply-queue"));
    }

    [Fact]
    public void CreateFromEnvelopeFallsBackToCorrelationIdWhenRequestIdIsMissing()
    {
        // Act
        var context = MessageContext.CreateFromEnvelope(
            new TestMessage("payload"),
            Envelope(requestId: null),
            routingKey: "rk",
            redelivered: false,
            retryCount: 0,
            replyToFallback: null,
            publisher: null,
            sendEndpointProvider: null,
            CancellationToken.None);

        // Assert
        context.RequestId.ShouldBe("corr-1");
    }

    [Fact]
    public async Task PublishOnAContextWithoutATransportThrows()
    {
        // Arrange
        var context = MessageContext.CreateFromEnvelope(
            new TestMessage("payload"),
            Envelope(),
            routingKey: "rk",
            redelivered: false,
            retryCount: 0,
            replyToFallback: null,
            publisher: null,
            sendEndpointProvider: null,
            CancellationToken.None);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => context.PublishAsync(new TestMessage("x")));
    }

    [Fact]
    public async Task SendOnAContextWithoutATransportThrows()
    {
        // Arrange
        var context = MessageContext.CreateFromEnvelope(
            new TestMessage("payload"),
            Envelope(),
            routingKey: "rk",
            redelivered: false,
            retryCount: 0,
            replyToFallback: null,
            publisher: null,
            sendEndpointProvider: null,
            CancellationToken.None);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => context.SendAsync(new Uri("queue:dest"), new TestMessage("x")));
    }
}
