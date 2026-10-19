using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Queues;
using Vulthil.Messaging.RabbitMq.HealthChecks;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class RabbitMqBusTopologyTests : BaseUnitTestCase
{
    private const string SingleActiveConsumerArgument = "x-single-active-consumer";

    private readonly Dictionary<string, IDictionary<string, object?>> _declaredQueues = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _declaredExchanges = new(StringComparer.Ordinal);

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
            .Setup(c => c.ExchangeDeclareAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback((string exchange, string type, bool _, bool _, IDictionary<string, object?> _, bool _, bool _, CancellationToken _) =>
                _declaredExchanges[exchange] = type)
            .Returns(Task.CompletedTask);
        channel
            .Setup(c => c.BasicConsumeAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("consumer-tag");

        GetMock<IConnection>()
            .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channel.Object);

        Use(new RabbitMqBusStartupStatus());
        Use<TimeProvider>(new FakeTimeProvider());
        Use<ILoggerFactory>(NullLoggerFactory.Instance);
        UseReal<DeliveryDispatcher>();
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
    public async Task FaultExchangeIsDeclaredAsADurableTopicExchange()
    {
        // Arrange
        var provider = ProviderConsumingOrderedEvents("plain");

        // Act
        await DeclareTopologyAsync(provider);

        // Assert
        _declaredExchanges.ShouldContainKeyAndValue("Fault.Exchange", ExchangeType.Topic);
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

    [Fact]
    public async Task QueueWithOnlyAnInMemoryRetryPolicyIsDeclaredWithoutTheRetryTopology()
    {
        // Arrange
        var provider = ProviderConsumingOrderedEvents("in-memory", configureQueue: queue => queue.UseRetry(retry =>
        {
            retry.Immediate(3);
            retry.InMemory();
        }));

        // Act
        await DeclareTopologyAsync(provider);

        // Assert
        _declaredQueues.ShouldContainKey("in-memory");
        _declaredQueues.ShouldNotContainKey("in-memory.Retry");
        _declaredExchanges.ShouldNotContainKey("in-memory.Retry");
    }

    [Fact]
    public async Task QueueWithADelayedRetryPolicyIsDeclaredWithTheRetryTopology()
    {
        // Arrange
        var provider = ProviderConsumingOrderedEvents("delayed", configureQueue: queue => queue.UseRetry(retry => retry.Immediate(3)));

        // Act
        await DeclareTopologyAsync(provider);

        // Assert
        _declaredQueues.ShouldContainKey("delayed.Retry");
        _declaredExchanges.ShouldContainKeyAndValue("delayed.Retry", ExchangeType.Topic);
    }

    [Fact]
    public async Task ConsumerWithADelayedRetryPolicyOnAQueueWithAnInMemoryDefaultGetsTheRetryTopology()
    {
        // Arrange
        var provider = TestProviders.Build(cfg => cfg.ConfigureQueue("mixed", queue =>
        {
            queue.UseRetry(retry =>
            {
                retry.Immediate(3);
                retry.InMemory();
            });
            queue.AddConsumer<OrderedConsumer>(consumer => consumer.UseRetry(retry => retry.Immediate(2)));
        }));

        // Act
        await DeclareTopologyAsync(provider);

        // Assert
        _declaredQueues.ShouldContainKey("mixed.Retry");
    }

    [Fact]
    public async Task ConsumerWithAnInMemoryRetryPolicyOnAQueueWithADelayedDefaultGetsNoRetryTopology()
    {
        // Arrange
        var provider = TestProviders.Build(cfg => cfg.ConfigureQueue("mixed", queue =>
        {
            queue.UseRetry(retry => retry.Immediate(3));
            queue.AddConsumer<OrderedConsumer>(consumer => consumer.UseRetry(retry =>
            {
                retry.Immediate(2);
                retry.InMemory();
            }));
        }));

        // Act
        await DeclareTopologyAsync(provider);

        // Assert
        _declaredQueues.ShouldContainKey("mixed");
        _declaredQueues.ShouldNotContainKey("mixed.Retry");
    }

    [Fact]
    public async Task PartitionedQueueWithADelayedRetryPolicyIsDeclaredWithoutTheRetryTopology()
    {
        // Arrange
        var provider = ProviderConsumingOrderedEvents(
            "ordered",
            configureQueue: queue => queue.UseRetry(retry => retry.Immediate(3)),
            configure: cfg => cfg.UsePartitioner<OrderedMessage>(new Partitioner(4), context => context.CorrelationId));

        // Act
        await DeclareTopologyAsync(provider);

        // Assert
        _declaredQueues.ShouldContainKey("ordered");
        _declaredQueues.ShouldNotContainKey("ordered.Retry");
        _declaredExchanges.ShouldNotContainKey("ordered.Retry");
    }

    [Fact]
    public async Task RequestConsumerQueueWithADelayedQueueDefaultIsDeclaredWithoutTheRetryTopology()
    {
        // Arrange
        var provider = TestProviders.Build(cfg => cfg.ConfigureQueue("quotes", queue =>
        {
            queue.UseRetry(retry => retry.Immediate(3));
            queue.AddRequestConsumer<QuoteConsumer>();
        }));

        // Act
        await DeclareTopologyAsync(provider);

        // Assert
        _declaredQueues.ShouldContainKey("quotes");
        _declaredQueues.ShouldNotContainKey("quotes.Retry");
    }

    internal sealed record OrderedMessage(string Key);

    internal sealed record QuoteRequest(string Sku);

    internal sealed record Quote(string Sku);

    private sealed class OrderedConsumer : IConsumer<OrderedMessage>
    {
        public Task ConsumeAsync(IMessageContext<OrderedMessage> messageContext, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class QuoteConsumer : IRequestConsumer<QuoteRequest, Quote>
    {
        public Task<Quote> ConsumeAsync(IMessageContext<QuoteRequest> messageContext, CancellationToken cancellationToken = default)
            => Task.FromResult(new Quote("sku"));
    }
}
