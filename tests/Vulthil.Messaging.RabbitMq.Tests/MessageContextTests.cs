using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.RabbitMq.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

/// <summary>
/// Represents the MessageContextTests.
/// </summary>
public sealed class MessageContextTests : BaseUnitTestCase
{
    private sealed record TestMessage(string Content);

    /// <summary>
    /// Executes this member.
    /// </summary>
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
        var context = MessageContext.CreateContext(eventArgs);

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

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void CreateContextShouldFallbackResponseAddressFromReplyTo()
    {
        // Arrange
        var eventArgs = CreateDeliverEventArgs(replyTo: "reply-queue");

        // Act
        var context = MessageContext.CreateContext(eventArgs);

        // Assert
        context.ResponseAddress.ShouldBe(new Uri("queue:reply-queue"));
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
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
        var context = MessageContext.CreateContext(eventArgs);

        // Assert
        context.CorrelationId.ShouldBe(string.Empty);
        context.RequestId.ShouldBeNull();
        context.Headers.ShouldNotBeNull();
        context.Headers.Count.ShouldBe(0);
        context.RetryCount.ShouldBe(0);
        context.ResponseAddress.ShouldBeNull();
        context.SentTime.ShouldBeNull();
        context.ExpirationTime.ShouldBeNull();
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    [Fact]
    public void CreateContextGenericShouldIncludeTypedMessage()
    {
        // Arrange
        var message = new TestMessage("payload");
        var eventArgs = CreateDeliverEventArgs(routingKey: "typed.route");

        // Act
        var context = MessageContext.CreateContext(message, eventArgs);

        // Assert
        context.Message.ShouldBe(message);
        context.RoutingKey.ShouldBe("typed.route");
        context.CorrelationId.ShouldBe("corr-1");
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
