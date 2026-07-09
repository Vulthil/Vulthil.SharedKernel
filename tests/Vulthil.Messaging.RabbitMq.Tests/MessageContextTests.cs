using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.RabbitMq.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class MessageContextTests : BaseUnitTestCase
{
    private sealed record TestMessage(string Content);

    [Fact]
    public void CreateContextShouldMapPropertiesHeadersAndTiming()
    {
        // Arrange
        var sentAt = DateTimeOffset.UtcNow;
        var eventArgs = CreateDeliverEventArgs(
            routingKey: "orders.created",
            redelivered: true,
            expiration: "5000",
            timestamp: sentAt.ToUnixTimeSeconds(),
            headers: new Dictionary<string, object?>
            {
                ["ConversationId"] = Encoding.UTF8.GetBytes("conv-1"),
                ["InitiatorId"] = Encoding.UTF8.GetBytes("init-1"),
                ["SourceAddress"] = Encoding.UTF8.GetBytes("source-queue"),
                ["DestinationAddress"] = Encoding.UTF8.GetBytes("amqp://broker/destination"),
                ["ResponseAddress"] = Encoding.UTF8.GetBytes("amqp://broker/reply"),
                ["FaultAddress"] = Encoding.UTF8.GetBytes("fault-queue"),
                ["x-retry-count"] = 3L
            });

        // Act
        var context = MessageContextFactory.CreateContext(new TestMessage("payload"), eventArgs);

        // Assert
        context.MessageId.ShouldBe("msg-1");
        context.CorrelationId.ShouldBe("corr-1");
        context.RequestId.ShouldBe("corr-1");
        context.RoutingKey.ShouldBe("orders.created");
        context.Redelivered.ShouldBeTrue();
        context.RetryCount.ShouldBe(3);
        context.ConversationId.ShouldBe("conv-1");
        context.InitiatorId.ShouldBe("init-1");
        context.SourceAddress.ShouldBe(new Uri("queue:source-queue"));
        context.DestinationAddress.ShouldBe(new Uri("amqp://broker/destination"));
        context.ResponseAddress.ShouldBe(new Uri("amqp://broker/reply"));
        context.FaultAddress.ShouldBe(new Uri("queue:fault-queue"));
        context.SentTime.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(sentAt.ToUnixTimeSeconds()));
        context.ExpirationTime.ShouldNotBeNull();
        context.ExpirationTime.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddSeconds(3));
        context.ExpirationTime.Value.ShouldBeLessThan(DateTimeOffset.UtcNow.AddSeconds(10));
    }

    [Fact]
    public void CreateContextNormalizesAmqpHeaderValuesToClrPrimitives()
    {
        // Arrange — the RabbitMQ client surfaces string headers (and strings nested in tables/arrays) as UTF-8 byte arrays.
        var eventArgs = CreateDeliverEventArgs(headers: new Dictionary<string, object?>
        {
            ["tenant"] = Encoding.UTF8.GetBytes("acme"),
            ["attempt"] = 3,
            ["big"] = 5_000_000_000L,
            ["critical"] = true,
            ["tags"] = new List<object?> { Encoding.UTF8.GetBytes("vip"), 7 },
            ["nested"] = new Dictionary<string, object?> { ["region"] = Encoding.UTF8.GetBytes("eu") },
        });

        // Act
        var context = MessageContextFactory.CreateContext(new TestMessage("payload"), eventArgs);

        // Assert
        context.Headers["tenant"].ShouldBeOfType<string>().ShouldBe("acme");
        context.Headers["attempt"].ShouldBeOfType<int>().ShouldBe(3);
        context.Headers["big"].ShouldBeOfType<long>().ShouldBe(5_000_000_000L);
        context.Headers["critical"].ShouldBeOfType<bool>().ShouldBe(true);
        var tags = context.Headers["tags"].ShouldBeAssignableTo<List<object?>>().ShouldNotBeNull();
        tags[0].ShouldBeOfType<string>().ShouldBe("vip");
        tags[1].ShouldBeOfType<int>().ShouldBe(7);
        var nested = context.Headers["nested"].ShouldBeAssignableTo<Dictionary<string, object?>>().ShouldNotBeNull();
        nested["region"].ShouldBeOfType<string>().ShouldBe("eu");
    }

    [Fact]
    public void CreateContextAnchorsTheTtlExpirationToTheSentTimestamp()
    {
        // Arrange — the AMQP expiration is a TTL relative to publish, carried alongside the publish timestamp.
        var sentTime = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
        var eventArgs = CreateDeliverEventArgs(expiration: "5000", timestamp: sentTime.ToUnixTimeSeconds());

        // Act
        var context = MessageContextFactory.CreateContext(new TestMessage("payload"), eventArgs);

        // Assert
        context.SentTime.ShouldBe(sentTime);
        context.ExpirationTime.ShouldBe(sentTime.AddSeconds(5));
    }

    [Fact]
    public void CreateContextFallsBackToTheConsumeClockForTtlWithoutASentTimestamp()
    {
        // Arrange
        var eventArgs = CreateDeliverEventArgs(expiration: "5000", timestamp: 0);

        // Act
        var context = MessageContextFactory.CreateContext(new TestMessage("payload"), eventArgs);

        // Assert
        context.SentTime.ShouldBeNull();
        var expiration = context.ExpirationTime.ShouldNotBeNull();
        expiration.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddSeconds(3));
        expiration.ShouldBeLessThan(DateTimeOffset.UtcNow.AddSeconds(10));
    }

    [Fact]
    public void CreateContextShouldFallbackResponseAddressFromReplyTo()
    {
        // Arrange
        var eventArgs = CreateDeliverEventArgs(replyTo: "reply-queue");

        // Act
        var context = MessageContextFactory.CreateContext(new TestMessage("payload"), eventArgs);

        // Assert
        context.ResponseAddress.ShouldBe(new Uri("queue:reply-queue"));
    }

    [Fact]
    public void CreateContextShouldUseDefaultsWhenPropertiesAreMissing()
    {
        // Arrange
        var eventArgs = CreateDeliverEventArgs(
            correlationId: null,
            headers: null,
            replyTo: null,
            expiration: null,
            timestamp: 0);

        // Act
        var context = MessageContextFactory.CreateContext(new TestMessage("payload"), eventArgs);

        // Assert
        context.CorrelationId.ShouldBeNull();
        context.RequestId.ShouldBeNull();
        context.Headers.ShouldNotBeNull();
        context.Headers.Count.ShouldBe(0);
        context.RetryCount.ShouldBe(0);
        context.ResponseAddress.ShouldBeNull();
        context.SentTime.ShouldBeNull();
        context.ExpirationTime.ShouldBeNull();
    }

    [Fact]
    public void CreateContextGenericShouldIncludeTypedMessage()
    {
        // Arrange
        var message = new TestMessage("payload");
        var eventArgs = CreateDeliverEventArgs(routingKey: "typed.route");

        // Act
        var context = MessageContextFactory.CreateContext(message, eventArgs);

        // Assert
        context.Message.ShouldBe(message);
        context.RoutingKey.ShouldBe("typed.route");
        context.CorrelationId.ShouldBe("corr-1");
    }

    [Fact]
    public void CreateSnapshotShouldCaptureTransportMetadata()
    {
        // Arrange
        var eventArgs = CreateDeliverEventArgs(
            routingKey: "orders.created",
            headers: new Dictionary<string, object?>
            {
                ["ConversationId"] = Encoding.UTF8.GetBytes("conv-1"),
                ["FaultAddress"] = Encoding.UTF8.GetBytes("fault-queue"),
                ["x-retry-count"] = 2L,
            });

        // Act
        var snapshot = MessageContextFactory.CreateSnapshot(eventArgs);

        // Assert
        snapshot.MessageId.ShouldBe("msg-1");
        snapshot.CorrelationId.ShouldBe("corr-1");
        snapshot.RequestId.ShouldBe("corr-1");
        snapshot.RoutingKey.ShouldBe("orders.created");
        snapshot.ConversationId.ShouldBe("conv-1");
        snapshot.FaultAddress.ShouldBe(new Uri("queue:fault-queue"));
        snapshot.RetryCount.ShouldBe(2);
    }

    private static BasicDeliverEventArgs CreateDeliverEventArgs(
        string routingKey = "route",
        bool redelivered = false,
        string? correlationId = "corr-1",
        string? replyTo = null,
        string? expiration = null,
        long timestamp = 0,
        IDictionary<string, object?>? headers = null)
    {
        var properties = new BasicProperties
        {
            MessageId = "msg-1",
            CorrelationId = correlationId,
            ReplyTo = replyTo,
            Headers = headers,
            Expiration = expiration,
            Timestamp = new AmqpTimestamp(timestamp)
        };

        return new BasicDeliverEventArgs(
            "consumer-tag",
            1,
            redelivered,
            "exchange",
            routingKey,
            properties,
            ReadOnlyMemory<byte>.Empty);
    }
}
