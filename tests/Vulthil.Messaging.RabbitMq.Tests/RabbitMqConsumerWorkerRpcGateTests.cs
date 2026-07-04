using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class RabbitMqConsumerWorkerRpcGateTests : BaseUnitTestCase
{
    [Fact]
    public async Task RpcRepliesSerializeWithAcksThroughTheChannelGateUnderParallelDispatch()
    {
        // Arrange
        var queue = new QueueDefinition("rpc-orders");
        queue.AddConsumer(new RequestConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(EchoRequestConsumer)),
            MessageType = new MessageType(typeof(TestRequest)),
            ResponseType = typeof(TestResponse),
        });

        Use(TestProviders.Build());
        Use<IEnumerable<IConsumeFilter<TestRequest>>>([]);
        Use(new EchoRequestConsumer());
        Use<IServiceScopeFactory>(new AutoMockerServiceScopeFactory(AutoMocker));
        Use<ILogger<RabbitMqConsumerWorker>>(NullLogger<RabbitMqConsumerWorker>.Instance);
        Use(queue);
        Use(0);
        Use(false);

        var typeCache = CreateInstance<MessageTypeCache>();
        typeCache.RegisterQueue(queue);
        Use(typeCache);

        var recorder = new ChannelWriteRecorder();
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
            .Returns((string _, string _, bool _, BasicProperties _, ReadOnlyMemory<byte> _, CancellationToken _) =>
                new ValueTask(recorder.RecordWriteAsync()));
        channel
            .Setup(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns((ulong _, bool _, CancellationToken _) => new ValueTask(recorder.RecordWriteAsync()));

        var worker = CreateInstance<RabbitMqConsumerWorker>();
        await worker.StartAsync(CancellationToken);

        // Act
        var deliveries = Enumerable.Range(1, 6)
            .Select(tag => DeliverRequestAsync(capturedConsumer!, (ulong)tag))
            .ToArray();
        await Task.WhenAll(deliveries);

        // Assert
        recorder.MaxObservedConcurrency.ShouldBe(1);
        channel.Verify(
            c => c.BasicPublishAsync(
                string.Empty, "reply.queue", true, It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(6));
        channel.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Exactly(6));
    }

    private static Task DeliverRequestAsync(IAsyncBasicConsumer consumer, ulong deliveryTag)
    {
        var properties = new BasicProperties
        {
            Type = typeof(TestRequest).FullName,
            ReplyTo = "reply.queue",
            CorrelationId = $"corr-{deliveryTag}",
            Headers = new Dictionary<string, object?>(),
        };
        return consumer.HandleBasicDeliverAsync(
            "consumer-tag",
            deliveryTag,
            false,
            "rpc-orders",
            "rpc-orders",
            properties,
            JsonSerializer.SerializeToUtf8Bytes(new TestRequest($"query-{deliveryTag}")),
            CancellationToken.None);
    }

    internal sealed record TestRequest(string Query);
    internal sealed record TestResponse(string Result);

    private sealed class EchoRequestConsumer : IRequestConsumer<TestRequest, TestResponse>
    {
        public Task<TestResponse> ConsumeAsync(IMessageContext<TestRequest> messageContext, CancellationToken cancellationToken = default)
            => Task.FromResult(new TestResponse(messageContext.Message.Query));
    }

    private sealed class ChannelWriteRecorder
    {
        private int _active;
        private int _maxObservedConcurrency;

        public int MaxObservedConcurrency => _maxObservedConcurrency;

        public async Task RecordWriteAsync()
        {
            var observed = Interlocked.Increment(ref _active);
            RecordObservedConcurrency(observed);
            await Task.Delay(10);
            Interlocked.Decrement(ref _active);
        }

        private void RecordObservedConcurrency(int observed)
        {
            var seen = Volatile.Read(ref _maxObservedConcurrency);
            while (observed > seen)
            {
                var previous = Interlocked.CompareExchange(ref _maxObservedConcurrency, observed, seen);
                if (previous == seen)
                {
                    return;
                }

                seen = previous;
            }
        }
    }
}
