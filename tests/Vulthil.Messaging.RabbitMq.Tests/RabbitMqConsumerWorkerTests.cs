using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.RabbitMq.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class RabbitMqConsumerWorkerTests : BaseUnitTestCase
{
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
