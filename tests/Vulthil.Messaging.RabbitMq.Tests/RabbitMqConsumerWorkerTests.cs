using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.RabbitMq.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class RabbitMqConsumerWorkerTests : BaseUnitTestCase
{
    private const string FaultExchange = "Fault.Exchange";
    private const string MessageUrn = "urn:message:Acme.Orders:OrderCreatedEvent";

    [Fact]
    public void ResolveFaultRouteBroadcastsToTheFaultExchangeWhenNoFaultAddressIsPresent()
    {
        // Arrange
        var headers = new Dictionary<string, object?>();

        // Act
        var (exchange, routingKey) = RabbitMqConsumerWorker.ResolveFaultRoute(headers, FaultExchange, MessageUrn);

        // Assert
        exchange.ShouldBe(FaultExchange);
        routingKey.ShouldBe(MessageUrn);
    }

    [Fact]
    public void ResolveFaultRouteRoutesPointToPointThroughTheDefaultExchangeWhenFaultAddressIsPresent()
    {
        // Arrange
        var headers = new Dictionary<string, object?> { ["FaultAddress"] = "queue:order-faults" };

        // Act
        var (exchange, routingKey) = RabbitMqConsumerWorker.ResolveFaultRoute(headers, FaultExchange, MessageUrn);

        // Assert
        exchange.ShouldBe(string.Empty);
        routingKey.ShouldBe("order-faults");
    }

    [Fact]
    public void ResolveFaultRouteReadsTheFaultAddressFromAWireEncodedHeaderValue()
    {
        // Arrange — RabbitMQ surfaces header values as UTF-8 byte arrays.
        var headers = new Dictionary<string, object?> { ["FaultAddress"] = Encoding.UTF8.GetBytes("queue:order-faults") };

        // Act
        var (exchange, routingKey) = RabbitMqConsumerWorker.ResolveFaultRoute(headers, FaultExchange, MessageUrn);

        // Assert
        exchange.ShouldBe(string.Empty);
        routingKey.ShouldBe("order-faults");
    }

    [Fact]
    public void WithRetryCountSurfacesTheAttemptThroughMessageContextRetryCount()
    {
        // Arrange — a delivery as first received, carrying no retry header.
        var delivery = new BasicDeliverEventArgs(
            consumerTag: "consumer-tag",
            deliveryTag: 7,
            redelivered: false,
            exchange: "exchange",
            routingKey: "rk",
            new BasicProperties { Headers = new Dictionary<string, object?>() },
            ReadOnlyMemory<byte>.Empty);

        // Act — the worker rewrites the delivery for the third in-memory attempt.
        var retried = RabbitMqConsumerWorker.WithRetryCount(delivery, 3);
        var context = MessageContextFactory.CreateContext(new TestMessage("payload"), retried);

        // Assert
        context.RetryCount.ShouldBe(3);
        retried.DeliveryTag.ShouldBe(7ul);
        retried.RoutingKey.ShouldBe("rk");
    }

    private sealed record TestMessage(string Value);
}
