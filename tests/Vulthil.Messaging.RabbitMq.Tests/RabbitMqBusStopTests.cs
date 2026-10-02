using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.RabbitMq.HealthChecks;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class RabbitMqBusStopTests : BaseUnitTestCase
{
    private readonly List<ConsumerChannel> _consumerChannels = [];
    private readonly Lazy<RabbitMqBus> _lazyTarget;
    private int _handlerHits;

    private RabbitMqBus Target => _lazyTarget.Value;

    public RabbitMqBusStopTests()
    {
        GetMock<IConnection>()
            .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateChannel);

        Use(new RabbitMqBusStartupStatus());
        Use<TimeProvider>(new FakeTimeProvider());
        Use<ILoggerFactory>(NullLoggerFactory.Instance);
        Use<ILogger<RabbitMqBus>>(NullLogger<RabbitMqBus>.Instance);
        Use(new DeliveryDispatcher(new AutoMockerServiceScopeFactory(AutoMocker), NullLogger<DeliveryDispatcher>.Instance));
        Use(new RecordingConsumer(() => Interlocked.Increment(ref _handlerHits)));
        Use<IEnumerable<IConsumeFilter<TestMessage>>>([]);

        _lazyTarget = new(CreateInstance<RabbitMqBus>);
    }

    protected override ValueTask Dispose() => _lazyTarget.IsValueCreated ? Target.DisposeAsync() : base.Dispose();

    [Fact]
    public async Task StoppingCancelsEveryConsumerAndClosesItsChannel()
    {
        // Arrange
        UseQueues("orders", "invoices");
        await Target.StartAsync(CancellationToken);

        // Act
        await Target.StopAsync(CancellationToken);

        // Assert
        _consumerChannels.Count.ShouldBe(2);
        _consumerChannels.ShouldAllBe(channel => channel.Cancels == 1 && channel.Disposals == 1);
    }

    [Fact]
    public async Task StartingAfterAStopConsumesAgainThroughNewConsumersOnly()
    {
        // Arrange
        UseQueues("orders");
        await Target.StartAsync(CancellationToken);
        await Target.StopAsync(CancellationToken);

        // Act
        await Target.StartAsync(CancellationToken);
        await DeliverAsync(_consumerChannels[^1]);

        // Assert
        _consumerChannels.Count.ShouldBe(2);
        _consumerChannels[0].Disposals.ShouldBe(1);
        _consumerChannels[1].Disposals.ShouldBe(0);
        _handlerHits.ShouldBe(1);
    }

    [Fact]
    public async Task AConsumerThatFailsToStopStillLetsTheOthersStopAndIsNotKeptForTheNextStart()
    {
        // Arrange
        UseQueues("orders", "invoices");
        await Target.StartAsync(CancellationToken);
        _consumerChannels[0].Mock
            .Setup(c => c.BasicCancelAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection closed"));

        // Act
        var exception = await Should.ThrowAsync<AggregateException>(() => Target.StopAsync(CancellationToken));
        await Target.StartAsync(CancellationToken);
        await Target.StopAsync(CancellationToken);

        // Assert
        exception.InnerExceptions.ShouldHaveSingleItem().ShouldBeOfType<InvalidOperationException>();
        _consumerChannels[1].Disposals.ShouldBe(1);
        _consumerChannels.Count.ShouldBe(4);
        _consumerChannels[0].Cancels.ShouldBe(1);
        _consumerChannels[2].Cancels.ShouldBe(1);
        _consumerChannels[3].Cancels.ShouldBe(1);
    }

    private void UseQueues(params string[] queueNames) =>
        Use(TestProviders.Build(cfg =>
        {
            foreach (var queueName in queueNames)
            {
                cfg.ConfigureQueue(queueName, queue => queue.AddConsumer<RecordingConsumer>());
            }
        }));

    private static Task DeliverAsync(ConsumerChannel channel) =>
        channel.Consumer!.HandleBasicDeliverAsync(
            "consumer-tag",
            1,
            false,
            "orders",
            "orders",
            new BasicProperties { Type = typeof(TestMessage).FullName, Headers = new Dictionary<string, object?>() },
            JsonSerializer.SerializeToUtf8Bytes(new TestMessage("payload")),
            CancellationToken.None);

    private IChannel CreateChannel()
    {
        var channel = new ConsumerChannel();
        channel.Mock
            .Setup(c => c.BasicConsumeAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
            .Callback((string _, bool _, string _, bool _, bool _, IDictionary<string, object?> _, IAsyncBasicConsumer consumer, CancellationToken _) =>
            {
                channel.Consumer = consumer;
                lock (_consumerChannels)
                {
                    _consumerChannels.Add(channel);
                }
            })
            .ReturnsAsync("consumer-tag");
        return channel.Mock.Object;
    }

    internal sealed record TestMessage(string Value);

    private sealed class RecordingConsumer(Action onConsume) : IConsumer<TestMessage>
    {
        public Task ConsumeAsync(IMessageContext<TestMessage> messageContext, CancellationToken cancellationToken = default)
        {
            onConsume();
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// A mocked channel that a consumer worker started on, with the consumer it registered and how often the worker
    /// canceled that consumer and disposed the channel.
    /// </summary>
    private sealed class ConsumerChannel
    {
        public Mock<IChannel> Mock { get; } = new();

        public IAsyncBasicConsumer? Consumer { get; set; }

        public int Cancels => Mock.Invocations.Count(invocation => invocation.Method.Name == nameof(IChannel.BasicCancelAsync));

        public int Disposals => Mock.Invocations.Count(invocation => invocation.Method.Name == nameof(IChannel.DisposeAsync));
    }
}
