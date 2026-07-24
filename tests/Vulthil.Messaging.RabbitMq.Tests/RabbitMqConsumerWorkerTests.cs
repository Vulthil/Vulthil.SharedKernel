using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq.Consumers;
using Vulthil.Messaging.Transport;
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

    [Fact]
    public async Task ProcessAsyncLeavesTheDeliveryUnsettledWhenCancelledDuringShutdown()
    {
        // Arrange
        var queue = new QueueDefinition("orders");
        queue.AddConsumer(new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(CancellingConsumer)),
            MessageType = new MessageType(typeof(TestMessage)),
        });

        Use(TestProviders.Build());
        Use<IEnumerable<IConsumeFilter<TestMessage>>>([]);
        Use(new CancellingConsumer());
        Use<IServiceScopeFactory>(new AutoMockerServiceScopeFactory(AutoMocker));
        Use<ILogger<RabbitMqConsumerWorker>>(NullLogger<RabbitMqConsumerWorker>.Instance);
        Use(queue);
        Use(0);
        Use(false);

        var typeCache = CreateInstance<MessageTypeCache>();
        typeCache.RegisterQueue(queue);
        Use(typeCache);

        IAsyncBasicConsumer? capturedConsumer = null;
        var channel = GetMock<IChannel>();
        channel
            .Setup(c => c.BasicConsumeAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
            .Callback((string _, bool _, string _, bool _, bool _, IDictionary<string, object?> _, IAsyncBasicConsumer consumer, CancellationToken _) =>
                capturedConsumer = consumer)
            .ReturnsAsync("consumer-tag");

        var worker = CreateInstance<RabbitMqConsumerWorker>();
        await worker.StartAsync(CancellationToken);
        capturedConsumer.ShouldNotBeNull();

        using var cancelledSource = new CancellationTokenSource();
        await cancelledSource.CancelAsync();

        // Act
        await capturedConsumer.HandleBasicDeliverAsync(
            "consumer-tag",
            1,
            false,
            "orders",
            "orders",
            new BasicProperties { Type = typeof(TestMessage).FullName, Headers = new Dictionary<string, object?>() },
            JsonSerializer.SerializeToUtf8Bytes(new TestMessage("payload")),
            cancelledSource.Token);

        // Assert
        channel.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        channel.Verify(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        channel.Verify(
            c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed record TestMessage(string Value);

    private sealed class CancellingConsumer : IConsumer<TestMessage>
    {
        public Task ConsumeAsync(IMessageContext<TestMessage> messageContext, CancellationToken cancellationToken = default)
            => throw new OperationCanceledException();
    }
}
