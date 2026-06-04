using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq.HealthChecks;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class RabbitMqBusTopologyTests : BaseUnitTestCase
{
    private const string SingleActiveConsumerArgument = "x-single-active-consumer";

    private readonly Dictionary<string, IDictionary<string, object?>> _declaredQueues = new(StringComparer.Ordinal);

    private readonly Lazy<RabbitMqBus> _lazyTarget;
    private RabbitMqBus Target => _lazyTarget.Value;

    public RabbitMqBusTopologyTests()
    {
        var channel = GetMock<IChannel>();
        channel
            .Setup(c => c.QueueDeclareAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback((string queue, bool _, bool _, bool _, IDictionary<string, object?> arguments, bool _, bool _, CancellationToken _) =>
                _declaredQueues[queue] = arguments)
            .ReturnsAsync(new QueueDeclareOk("queue", 0, 0));
        channel
            .Setup(c => c.BasicConsumeAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("consumer-tag");

        GetMock<IConnection>()
            .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel.Object);

        Use(new RabbitMqBusStartupStatus());
        Use<ILoggerFactory>(NullLoggerFactory.Instance);
        Use<ILogger<RabbitMqBus>>(NullLogger<RabbitMqBus>.Instance);

        _lazyTarget = new(CreateInstance<RabbitMqBus>);
    }

    protected override ValueTask Dispose() => _lazyTarget.IsValueCreated ? Target.DisposeAsync() : base.Dispose();

    private Task DeclareTopologyAsync(IMessageConfigurationProvider provider)
    {
        Use(provider);
        return Target.StartAsync(CancellationToken);
    }

    private static IMessageConfigurationProvider ProviderConsumingOrderedEvents(
        string queueName,
        Action<IQueueConfigurator>? configureQueue = null,
        Action<IMessagingConfigurator>? configure = null)
        => TestProviders.Build(cfg =>
        {
            cfg.ConfigureQueue(queueName, queue =>
            {
                queue.AddConsumer<OrderedConsumer>();
                configureQueue?.Invoke(queue);
            });
            configure?.Invoke(cfg);
        });

    [Fact]
    public async Task PlainQueueIsDeclaredWithoutSingleActiveConsumerArgument()
    {
        // Arrange
        var provider = ProviderConsumingOrderedEvents("plain");

        // Act
        await DeclareTopologyAsync(provider);

        // Assert
        _declaredQueues.ShouldContainKey("plain");
        _declaredQueues["plain"].ShouldNotContainKey(SingleActiveConsumerArgument);
    }

    [Fact]
    public async Task ExplicitlyConfiguredQueueIsDeclaredWithSingleActiveConsumerArgument()
    {
        // Arrange
        var provider = ProviderConsumingOrderedEvents("sole", configureQueue: queue => queue.UseSingleActiveConsumer());

        // Act
        await DeclareTopologyAsync(provider);

        // Assert
        _declaredQueues["sole"][SingleActiveConsumerArgument].ShouldBe(true);
    }

    [Fact]
    public async Task PartitionedQueueAutomaticallyEnablesSingleActiveConsumer()
    {
        // Arrange
        var provider = ProviderConsumingOrderedEvents(
            "ordered",
            configure: cfg => cfg.UsePartitioner<OrderedMessage>(new Partitioner(4), context => context.CorrelationId));

        // Act
        await DeclareTopologyAsync(provider);

        // Assert
        _declaredQueues["ordered"][SingleActiveConsumerArgument].ShouldBe(true);
    }

    internal sealed record OrderedMessage(string Key);

    private sealed class OrderedConsumer : IConsumer<OrderedMessage>
    {
        public Task ConsumeAsync(IMessageContext<OrderedMessage> messageContext, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
