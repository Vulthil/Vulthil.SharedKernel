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

public sealed class RabbitMqConsumerWorkerFaultPayloadTests : BaseUnitTestCase
{
    private RecordedPublish? _fault;

    [Fact]
    public async Task FaultForAnEnvelopeDeliveryCarriesTheOriginalMessageNotTheEnvelope()
    {
        // Arrange
        var provider = TestProviders.Build();
        var urn = provider.GetUrn(typeof(TestMessage));
        var envelope = MessageEnvelopeFactory.Create(
            new TestMessage("payload"), new PublishContext(), "message-id-1", "corr-1", urn, provider.JsonSerializerOptions);
        var consumer = await StartWorkerAsync(provider);

        // Act
        await consumer.HandleBasicDeliverAsync(
            "consumer-tag",
            1,
            false,
            "orders",
            "orders",
            new BasicProperties { Type = urn.AbsoluteUri, MessageId = "message-id-1", CorrelationId = "corr-1", Headers = new Dictionary<string, object?>() },
            JsonSerializer.SerializeToUtf8Bytes(envelope, provider.JsonSerializerOptions),
            CancellationToken);

        // Assert
        var published = _fault.ShouldNotBeNull();
        published.Exchange.ShouldBe("Fault.Exchange");
        published.RoutingKey.ShouldBe(urn.AbsoluteUri);
        var fault = JsonSerializer.Deserialize<Fault<TestMessage>>(published.Body, provider.JsonSerializerOptions).ShouldNotBeNull();
        fault.Message.Value.ShouldBe("payload");
        fault.ExceptionMessage.ShouldBe("consumer exploded");
        fault.ExceptionType.ShouldBe(typeof(InvalidOperationException).FullName);
        fault.OriginalContext.MessageId.ShouldBe("message-id-1");
        fault.OriginalContext.CorrelationId.ShouldBe("corr-1");
    }

    [Fact]
    public async Task FaultForABareJsonDeliveryCarriesTheWholeBody()
    {
        // Arrange
        var provider = TestProviders.Build();
        var consumer = await StartWorkerAsync(provider);

        // Act
        await consumer.HandleBasicDeliverAsync(
            "consumer-tag",
            1,
            false,
            "orders",
            "orders",
            new BasicProperties { Type = typeof(TestMessage).FullName, MessageId = "message-id-2", Headers = new Dictionary<string, object?>() },
            JsonSerializer.SerializeToUtf8Bytes(new TestMessage("bare"), provider.JsonSerializerOptions),
            CancellationToken);

        // Assert
        var published = _fault.ShouldNotBeNull();
        var fault = JsonSerializer.Deserialize<Fault<TestMessage>>(published.Body, provider.JsonSerializerOptions).ShouldNotBeNull();
        fault.Message.Value.ShouldBe("bare");
    }

    private async Task<IAsyncBasicConsumer> StartWorkerAsync(IMessageConfigurationProvider provider)
    {
        var queue = new QueueDefinition("orders");
        queue.AddConsumer(new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(ThrowingConsumer)),
            MessageType = new MessageType(typeof(TestMessage)),
        });

        Use(provider);
        Use<IEnumerable<IConsumeFilter<TestMessage>>>([]);
        Use(new ThrowingConsumer());
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
        channel
            .Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Callback((string exchange, string routingKey, bool _, BasicProperties props, ReadOnlyMemory<byte> body, CancellationToken _) =>
                _fault = new RecordedPublish(exchange, routingKey, props, body.ToArray()))
            .Returns(ValueTask.CompletedTask);

        var worker = CreateInstance<RabbitMqConsumerWorker>();
        await worker.StartAsync(CancellationToken);
        return capturedConsumer.ShouldNotBeNull();
    }

    private sealed record RecordedPublish(string Exchange, string RoutingKey, BasicProperties Properties, byte[] Body);

    private sealed record TestMessage(string Value);

    private sealed class ThrowingConsumer : IConsumer<TestMessage>
    {
        public Task ConsumeAsync(IMessageContext<TestMessage> messageContext, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("consumer exploded");
    }
}
