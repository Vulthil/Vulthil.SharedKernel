using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.RabbitMq.HealthChecks;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class RabbitMqBusDispatchScopingTests : BaseUnitTestCase
{
    private readonly Mock<IChannel> _channel = new();
    private readonly Dictionary<string, IAsyncBasicConsumer> _consumersByQueue = new(StringComparer.Ordinal);
    private readonly Lazy<RabbitMqBus> _lazyTarget;

    private RabbitMqBus Target => _lazyTarget.Value;

    public RabbitMqBusDispatchScopingTests()
    {
        _channel
            .Setup(c => c.BasicConsumeAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
            .Callback((string queue, bool _, string _, bool _, bool _, IDictionary<string, object?> _, IAsyncBasicConsumer consumer, CancellationToken _) =>
                _consumersByQueue[queue] = consumer)
            .ReturnsAsync("consumer-tag");

        GetMock<IConnection>()
            .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_channel.Object);

        Use(new RabbitMqBusStartupStatus());
        Use<ILoggerFactory>(NullLoggerFactory.Instance);
        Use<ILogger<RabbitMqBus>>(NullLogger<RabbitMqBus>.Instance);
        Use<IServiceScopeFactory>(new AutoMockerServiceScopeFactory(AutoMocker));
        Use<IEnumerable<IConsumeFilter<SharedEvent>>>([]);

        _lazyTarget = new(CreateInstance<RabbitMqBus>);
    }

    protected override ValueTask Dispose() => _lazyTarget.IsValueCreated ? Target.DisposeAsync() : base.Dispose();

    private static IMessageConfigurationProvider ProviderWithTwoQueuesConsumingSharedEvent()
        => TestProviders.Build(cfg =>
        {
            cfg.ConfigureQueue("alpha", queue => queue.AddConsumer<AlphaConsumer>());
            cfg.ConfigureQueue("beta", queue => queue.AddConsumer<BetaConsumer>());
        });

    private static Task DeliverSharedEventAsync(IAsyncBasicConsumer consumer, ulong deliveryTag)
        => consumer.HandleBasicDeliverAsync(
            "consumer-tag",
            deliveryTag,
            false,
            "exchange",
            "routing-key",
            new BasicProperties { Type = typeof(SharedEvent).FullName, Headers = new Dictionary<string, object?>() },
            JsonSerializer.SerializeToUtf8Bytes(new SharedEvent("payload")),
            CancellationToken.None);

    [Fact]
    public async Task DeliveryOnOneQueueDoesNotDispatchAnotherQueuesConsumers()
    {
        // Arrange
        var alphaHits = 0;
        var betaHits = 0;
        Use(new AlphaConsumer(() => alphaHits++));
        Use(new BetaConsumer(() => betaHits++));
        Use(ProviderWithTwoQueuesConsumingSharedEvent());
        await Target.StartAsync(CancellationToken);

        // Act
        await DeliverSharedEventAsync(_consumersByQueue["alpha"], 1);

        // Assert
        alphaHits.ShouldBe(1);
        betaHits.ShouldBe(0);
    }

    [Fact]
    public async Task PublishFannedOutToTwoQueuesRunsEachQueuesConsumerExactlyOnce()
    {
        // Arrange
        var alphaHits = 0;
        var betaHits = 0;
        Use(new AlphaConsumer(() => alphaHits++));
        Use(new BetaConsumer(() => betaHits++));
        Use(ProviderWithTwoQueuesConsumingSharedEvent());
        await Target.StartAsync(CancellationToken);

        // Act
        await DeliverSharedEventAsync(_consumersByQueue["alpha"], 1);
        await DeliverSharedEventAsync(_consumersByQueue["beta"], 2);

        // Assert
        alphaHits.ShouldBe(1);
        betaHits.ShouldBe(1);
    }

    [Fact]
    public void BuildTypeCachesScopesUrnPlanLookupsToTheOwningQueue()
    {
        // Arrange
        var provider = TestProviders.Build(cfg =>
        {
            cfg.ConfigureQueue("alpha", queue => queue.AddConsumer<AlphaConsumer>());
            cfg.ConfigureQueue("beta", queue =>
            {
                queue.AddConsumer<BetaConsumer>();
                queue.AddConsumer<BetaOnlyConsumer>();
            });
        });
        Use(provider);
        var sharedEventUrn = provider.GetUrn(typeof(SharedEvent));
        var betaOnlyEventUrn = provider.GetUrn(typeof(BetaOnlyEvent));

        // Act
        var typeCaches = Target.BuildTypeCaches(provider.QueueDefinitions);

        // Assert
        typeCaches["alpha"].GetPlanByUrn(sharedEventUrn)!.Handlers.ShouldHaveSingleItem();
        typeCaches["beta"].GetPlanByUrn(sharedEventUrn)!.Handlers.ShouldHaveSingleItem();
        typeCaches["alpha"].GetPlanByUrn(betaOnlyEventUrn).ShouldBeNull();
        typeCaches["beta"].GetPlanByUrn(betaOnlyEventUrn).ShouldNotBeNull();
    }

    internal sealed record SharedEvent(string Value);
    internal sealed record BetaOnlyEvent(string Value);

    private sealed class AlphaConsumer(Action onConsume) : IConsumer<SharedEvent>
    {
        public Task ConsumeAsync(IMessageContext<SharedEvent> messageContext, CancellationToken cancellationToken = default)
        {
            onConsume();
            return Task.CompletedTask;
        }
    }

    private sealed class BetaConsumer(Action onConsume) : IConsumer<SharedEvent>
    {
        public Task ConsumeAsync(IMessageContext<SharedEvent> messageContext, CancellationToken cancellationToken = default)
        {
            onConsume();
            return Task.CompletedTask;
        }
    }

    private sealed class BetaOnlyConsumer : IConsumer<BetaOnlyEvent>
    {
        public Task ConsumeAsync(IMessageContext<BetaOnlyEvent> messageContext, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
