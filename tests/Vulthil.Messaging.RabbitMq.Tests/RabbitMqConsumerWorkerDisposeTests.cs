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

public sealed class RabbitMqConsumerWorkerDisposeTests : BaseUnitTestCase
{
    private const string QueueName = "orders";

    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(30);

    private readonly QueueDefinition _queue = new(QueueName);
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly BlockingConsumer _consumer = new();
    private readonly Mock<IChannel> _channel;

    private IAsyncBasicConsumer? _capturedConsumer;
    private int _ackCount;

    public RabbitMqConsumerWorkerDisposeTests()
    {
        _queue.AddConsumer(new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(BlockingConsumer)),
            MessageType = new MessageType(typeof(OrderMessage)),
        });

        Use(TestProviders.Build());
        Use<IEnumerable<IConsumeFilter<OrderMessage>>>([]);
        Use(_consumer);
        Use<IServiceScopeFactory>(new AutoMockerServiceScopeFactory(AutoMocker));
        Use<ILogger<RabbitMqConsumerWorker>>(NullLogger<RabbitMqConsumerWorker>.Instance);
        Use<TimeProvider>(_timeProvider);
        Use(_queue);
        Use(0);
        Use(true);

        _channel = GetMock<IChannel>();
        _channel
            .Setup(c => c.BasicConsumeAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
            .Callback((string _, bool _, string _, bool _, bool _, IDictionary<string, object?> _, IAsyncBasicConsumer consumer, CancellationToken _) =>
                _capturedConsumer = consumer)
            .ReturnsAsync("consumer-tag");
        _channel
            .Setup(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback(() => _ackCount++)
            .Returns(ValueTask.CompletedTask);
    }

    private async Task<RabbitMqConsumerWorker> StartWorkerAsync()
    {
        var typeCache = CreateInstance<MessageTypeCache>();
        typeCache.RegisterQueue(_queue);
        Use(typeCache);

        var worker = CreateInstance<RabbitMqConsumerWorker>();
        await worker.StartAsync(CancellationToken);
        return worker;
    }

    private async Task DeliverAndBlockAsync()
    {
        var consumer = _capturedConsumer.ShouldNotBeNull();
        await consumer.HandleBasicDeliverAsync(
            "consumer-tag",
            1,
            false,
            QueueName,
            QueueName,
            new BasicProperties { Type = typeof(OrderMessage).FullName, Headers = new Dictionary<string, object?>() },
            JsonSerializer.SerializeToUtf8Bytes(new OrderMessage("order-1")),
            CancellationToken);
        await _consumer.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
    }

    [Fact]
    public async Task DisposeAsyncWaitsForAnInFlightLaneToSettleBeforeClosingTheChannel()
    {
        // Arrange
        var worker = await StartWorkerAsync();
        await DeliverAndBlockAsync();

        // Act
        var disposal = worker.DisposeAsync().AsTask();
        var closedBeforeTheLaneSettled = disposal.IsCompleted;
        _consumer.Release.SetResult();
        await disposal.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        // Assert
        closedBeforeTheLaneSettled.ShouldBeFalse();
        _ackCount.ShouldBe(1);
        _channel.Verify(c => c.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsyncStopsWaitingAtTheDrainTimeoutAndLeavesTheChannelToTheConnection()
    {
        // Arrange
        var worker = await StartWorkerAsync();
        await DeliverAndBlockAsync();

        // Act
        var disposal = worker.DisposeAsync().AsTask();
        _timeProvider.Advance(DrainTimeout - TimeSpan.FromSeconds(1));
        var returnedBeforeTheTimeout = disposal.IsCompleted;
        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        await disposal.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        // Assert
        returnedBeforeTheTimeout.ShouldBeFalse();
        _ackCount.ShouldBe(0);
        _channel.Verify(c => c.DisposeAsync(), Times.Never);
    }

    [Fact]
    public async Task DisposeAsyncSwallowsAChannelAlreadyDisposedByAutoRecovery()
    {
        // Arrange
        _channel
            .Setup(c => c.BasicCancelAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ObjectDisposedException(nameof(IChannel)));
        var worker = await StartWorkerAsync();

        // Act & Assert
        await Should.NotThrowAsync(() => worker.DisposeAsync().AsTask());
    }

    public sealed record OrderMessage(string Id);

    public sealed class BlockingConsumer : IConsumer<OrderMessage>
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ConsumeAsync(IMessageContext<OrderMessage> messageContext, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Release.Task;
        }
    }
}
