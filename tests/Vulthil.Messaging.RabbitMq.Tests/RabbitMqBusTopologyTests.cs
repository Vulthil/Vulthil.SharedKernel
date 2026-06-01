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
    }

    private async Task DeclareTopologyAsync(MessagingOptions options)
    {
        Use<IMessageConfigurationProvider>(options);
        await using var bus = CreateInstance<RabbitMqBus>();
        await bus.StartAsync(CancellationToken);
    }

    private static MessagingOptions OptionsConsumingOrderedEvents(string queueName)
    {
        var queue = new QueueDefinition(queueName);
        queue.AddConsumer(new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(OrderedConsumer)),
            MessageType = new MessageType(typeof(OrderedMessage)),
        });

        var options = new MessagingOptions();
        options.QueueDefinitions[queue.Name] = queue;
        return options;
    }

    [Fact]
    public async Task PlainQueueIsDeclaredWithoutSingleActiveConsumerArgument()
    {
        // Arrange
        var options = OptionsConsumingOrderedEvents("plain");

        // Act
        await DeclareTopologyAsync(options);

        // Assert
        _declaredQueues.ShouldContainKey("plain");
        _declaredQueues["plain"].ShouldNotContainKey(SingleActiveConsumerArgument);
    }

    [Fact]
    public async Task ExplicitlyConfiguredQueueIsDeclaredWithSingleActiveConsumerArgument()
    {
        // Arrange
        var options = OptionsConsumingOrderedEvents("sole");
        options.QueueDefinitions["sole"].SingleActiveConsumer = true;

        // Act
        await DeclareTopologyAsync(options);

        // Assert
        _declaredQueues["sole"][SingleActiveConsumerArgument].ShouldBe(true);
    }

    [Fact]
    public async Task PartitionedQueueAutomaticallyEnablesSingleActiveConsumer()
    {
        // Arrange
        var options = OptionsConsumingOrderedEvents("ordered");
        options.RegisterPartition(
            typeof(OrderedMessage),
            new PartitionSpec(new Partitioner(4), (Func<IMessageContext<OrderedMessage>, string?>)(context => context.CorrelationId)));

        // Act
        await DeclareTopologyAsync(options);

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
