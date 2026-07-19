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

public sealed class RabbitMqConsumerWorkerRetryTests : BaseUnitTestCase
{
    private const string QueueName = "orders";
    private const string RetryExchange = "orders.Retry";
    private const string FaultExchange = "Fault.Exchange";
    private const string RetryCountHeader = "x-retry-count";
    private const string RetryHandlersHeader = "x-retry-handlers";

    private static readonly string[] _ghostHandlerIdentities = ["Ghost.Consumer:Ghost.Message"];

    private readonly QueueDefinition _queue = new(QueueName);
    private readonly List<CapturedPublish> _publishes = [];
    private readonly Mock<IChannel> _channel;

    private IAsyncBasicConsumer? _capturedConsumer;
    private int _ackCount;
    private int _nackCount;

    public RabbitMqConsumerWorkerRetryTests()
    {
        Use(TestProviders.Build());
        Use<IEnumerable<IConsumeFilter<OrderMessage>>>([]);
        Use<IEnumerable<IConsumeFilter<IOrderEvent>>>([]);
        Use<IEnumerable<IConsumeFilter<PricingRequest>>>([]);
        Use<IServiceScopeFactory>(new AutoMockerServiceScopeFactory(AutoMocker));
        Use<ILogger<RabbitMqConsumerWorker>>(NullLogger<RabbitMqConsumerWorker>.Instance);
        Use(_queue);
        Use(0);
        Use(false);

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
        _channel
            .Setup(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback(() => _nackCount++)
            .Returns(ValueTask.CompletedTask);
        _channel
            .Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Callback((string exchange, string routingKey, bool _, BasicProperties props, ReadOnlyMemory<byte> body, CancellationToken _) =>
                _publishes.Add(new CapturedPublish(exchange, routingKey, props, body.ToArray())))
            .Returns(ValueTask.CompletedTask);
    }

    private static string HandlerIdentity(Type consumerType, Type messageType)
        => $"{consumerType.FullName}:{messageType.FullName}";

    private static RetryPolicyDefinition BuildPolicy(Action<RetryPolicyConfigurator> configure)
    {
        var configurator = new RetryPolicyConfigurator();
        configure(configurator);
        return configurator.Build();
    }

    private void RegisterConsumer<TConsumer, TMessage>(RetryPolicyDefinition? retryPolicy = null)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : notnull
        => _queue.AddConsumer(new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(TConsumer)),
            MessageType = new MessageType(typeof(TMessage)),
            RetryPolicy = retryPolicy,
        });

    private async Task<RabbitMqConsumerWorker> StartWorkerAsync()
    {
        var typeCache = CreateInstance<MessageTypeCache>();
        typeCache.RegisterQueue(_queue);
        Use(typeCache);

        var worker = CreateInstance<RabbitMqConsumerWorker>();
        await worker.StartAsync(CancellationToken);
        return worker;
    }

    private Task DeliverAsync<TMessage>(TMessage message, IDictionary<string, object?>? headers = null, string? replyTo = null, string? correlationId = null)
        where TMessage : notnull
        => _capturedConsumer!.HandleBasicDeliverAsync(
            "consumer-tag",
            1,
            false,
            QueueName,
            QueueName,
            new BasicProperties
            {
                Type = typeof(TMessage).FullName,
                Headers = headers ?? new Dictionary<string, object?>(),
                ReplyTo = replyTo,
                CorrelationId = correlationId,
            },
            JsonSerializer.SerializeToUtf8Bytes(message),
            CancellationToken);

    [Fact]
    public async Task PolymorphicConsumersRetryPolicyAppliesToConcreteMessages()
    {
        // Arrange — a polymorphic consumer registered for the interface, subscribed to a concrete implementer.
        var consumer = new PolymorphicOrderEventConsumer { FailuresBeforeSuccess = 2 };
        Use(consumer);
        _queue.AddSubscription(new Subscription(new MessageType(typeof(ConcreteOrderEvent))));
        _queue.AddConsumer(new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(PolymorphicOrderEventConsumer)),
            MessageType = new MessageType(typeof(IOrderEvent)),
            RetryPolicy = BuildPolicy(r =>
            {
                r.Immediate(2);
                r.InMemory();
            }),
        });
        await StartWorkerAsync();

        // Act
        await DeliverAsync(new ConcreteOrderEvent("evt-1"));

        // Assert — the interface registration's policy governs the concrete delivery: two in-memory retries, then success.
        consumer.Attempts.ShouldBe(3);
        _ackCount.ShouldBe(1);
        _nackCount.ShouldBe(0);
        _publishes.ShouldBeEmpty();
    }

    [Fact]
    public async Task OnlyTheFailingConsumerIsRedispatchedOnInMemoryRetry()
    {
        // Arrange — two consumers on one message; only one of them fails, terminally.
        var steady = new SteadyConsumer();
        var failing = new FailingConsumer { FailuresBeforeSuccess = int.MaxValue };
        Use(steady);
        Use(failing);
        RegisterConsumer<SteadyConsumer, OrderMessage>();
        RegisterConsumer<FailingConsumer, OrderMessage>(BuildPolicy(r =>
        {
            r.Immediate(1);
            r.InMemory();
        }));
        await StartWorkerAsync();

        // Act
        await DeliverAsync(new OrderMessage("order-1"));

        // Assert — the steady consumer ran exactly once; the failing one used its retry, faulted, and the delivery nacked.
        steady.Attempts.ShouldBe(1);
        failing.Attempts.ShouldBe(2);
        _ackCount.ShouldBe(0);
        _nackCount.ShouldBe(1);
        _publishes.ShouldHaveSingleItem().Exchange.ShouldBe(FaultExchange);
    }

    [Fact]
    public async Task OnlyTheFailingConsumerIsRedispatchedWhenItRecoversInMemory()
    {
        // Arrange — the failing consumer recovers on its second attempt.
        var steady = new SteadyConsumer();
        var failing = new FailingConsumer { FailuresBeforeSuccess = 1 };
        Use(steady);
        Use(failing);
        RegisterConsumer<SteadyConsumer, OrderMessage>();
        RegisterConsumer<FailingConsumer, OrderMessage>(BuildPolicy(r =>
        {
            r.Immediate(2);
            r.InMemory();
        }));
        await StartWorkerAsync();

        // Act
        await DeliverAsync(new OrderMessage("order-2"));

        // Assert
        steady.Attempts.ShouldBe(1);
        failing.Attempts.ShouldBe(2);
        _ackCount.ShouldBe(1);
        _nackCount.ShouldBe(0);
        _publishes.ShouldBeEmpty();
    }

    [Fact]
    public async Task DelayedRetryRepublishListsOnlyTheFailedHandlers()
    {
        // Arrange — a delayed (non-in-memory) policy on the failing consumer only.
        var steady = new SteadyConsumer();
        var failing = new FailingConsumer { FailuresBeforeSuccess = int.MaxValue };
        Use(steady);
        Use(failing);
        RegisterConsumer<SteadyConsumer, OrderMessage>();
        RegisterConsumer<FailingConsumer, OrderMessage>(BuildPolicy(r => r.SetIntervals(TimeSpan.FromSeconds(5))));
        await StartWorkerAsync();

        // Act
        await DeliverAsync(new OrderMessage("order-3"));

        // Assert — one republish to the retry queue carrying the failed handler's identity and the next retry round.
        steady.Attempts.ShouldBe(1);
        failing.Attempts.ShouldBe(1);
        _ackCount.ShouldBe(1);
        _nackCount.ShouldBe(0);

        var republish = _publishes.ShouldHaveSingleItem();
        republish.Exchange.ShouldBe(RetryExchange);
        republish.RoutingKey.ShouldBe(QueueName);
        republish.Properties.Expiration.ShouldBe("5000");
        republish.Properties.Headers!.ShouldContainKeyAndValue(RetryCountHeader, 1);

        var listed = republish.Properties.Headers![RetryHandlersHeader].ShouldBeOfType<string>();
        listed.ShouldBe(JsonSerializer.Serialize(new[] { HandlerIdentity(typeof(FailingConsumer), typeof(OrderMessage)) }));
    }

    [Fact]
    public async Task DelayedRetryRedeliveryDispatchesOnlyTheListedHandlers()
    {
        // Arrange — a re-delivery stamped with only the previously-failed handler's identity.
        var steady = new SteadyConsumer();
        var failing = new FailingConsumer { FailuresBeforeSuccess = 0 };
        Use(steady);
        Use(failing);
        RegisterConsumer<SteadyConsumer, OrderMessage>();
        RegisterConsumer<FailingConsumer, OrderMessage>(BuildPolicy(r => r.SetIntervals(TimeSpan.FromSeconds(5))));
        await StartWorkerAsync();

        var headers = new Dictionary<string, object?>
        {
            [RetryCountHeader] = 1,
            [RetryHandlersHeader] = JsonSerializer.Serialize(new[] { HandlerIdentity(typeof(FailingConsumer), typeof(OrderMessage)) }),
        };

        // Act
        await DeliverAsync(new OrderMessage("order-4"), headers);

        // Assert — the consumer that already succeeded on the first delivery is not re-run.
        steady.Attempts.ShouldBe(0);
        failing.Attempts.ShouldBe(1);
        _ackCount.ShouldBe(1);
        _nackCount.ShouldBe(0);
        _publishes.ShouldBeEmpty();
    }

    [Fact]
    public async Task RedeliveryListingOnlyUnknownHandlersIsAckedWithoutDispatch()
    {
        // Arrange — the listed handler no longer exists (renamed or removed between republish and re-delivery).
        var steady = new SteadyConsumer();
        Use(steady);
        RegisterConsumer<SteadyConsumer, OrderMessage>(BuildPolicy(r => r.SetIntervals(TimeSpan.FromSeconds(5))));
        await StartWorkerAsync();

        var headers = new Dictionary<string, object?>
        {
            [RetryCountHeader] = 1,
            [RetryHandlersHeader] = JsonSerializer.Serialize(_ghostHandlerIdentities),
        };

        // Act
        await DeliverAsync(new OrderMessage("order-5"), headers);

        // Assert
        steady.Attempts.ShouldBe(0);
        _ackCount.ShouldBe(1);
        _nackCount.ShouldBe(0);
        _publishes.ShouldBeEmpty();
    }

    [Fact]
    public async Task RequestConsumerFailureRepliesWithAFaultOnceAndNeverRetries()
    {
        // Arrange — a queue-level retry policy that must NOT re-run the request consumer.
        var consumer = new ExplodingPricingConsumer();
        Use(consumer);
        _queue.DefaultRetryPolicy = BuildPolicy(r =>
        {
            r.Immediate(3);
            r.InMemory();
        });
        _queue.AddConsumer(new RequestConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(ExplodingPricingConsumer)),
            MessageType = new MessageType(typeof(PricingRequest)),
            ResponseType = typeof(PricingReply),
        });
        await StartWorkerAsync();

        // Act
        await DeliverAsync(new PricingRequest("sku-1"), replyTo: "reply-q", correlationId: "corr-1");

        // Assert — invoked once, one RPC fault reply through the default exchange, delivery acked, no broker fault.
        consumer.Attempts.ShouldBe(1);
        _ackCount.ShouldBe(1);
        _nackCount.ShouldBe(0);

        var reply = _publishes.ShouldHaveSingleItem();
        reply.Exchange.ShouldBe(string.Empty);
        reply.RoutingKey.ShouldBe("reply-q");
        var envelope = JsonSerializer.Deserialize<MessageEnvelope>(reply.Body)!;
        envelope.MessageType.ShouldBe(RpcFault.UrnUri);
    }

    private sealed record CapturedPublish(string Exchange, string RoutingKey, BasicProperties Properties, byte[] Body);

    public interface IOrderEvent
    {
        string Id { get; }
    }

    public sealed record ConcreteOrderEvent(string Id) : IOrderEvent;

    public sealed record OrderMessage(string Id);

    public sealed record PricingRequest(string Sku);

    public sealed record PricingReply(string Sku, decimal Price);

    public sealed class PolymorphicOrderEventConsumer : IConsumer<IOrderEvent>
    {
        public int FailuresBeforeSuccess { get; set; }

        public int Attempts { get; private set; }

        public Task ConsumeAsync(IMessageContext<IOrderEvent> messageContext, CancellationToken cancellationToken = default)
        {
            Attempts++;
            return Attempts <= FailuresBeforeSuccess
                ? throw new InvalidOperationException($"attempt {Attempts} failed")
                : Task.CompletedTask;
        }
    }

    public sealed class SteadyConsumer : IConsumer<OrderMessage>
    {
        public int Attempts { get; private set; }

        public Task ConsumeAsync(IMessageContext<OrderMessage> messageContext, CancellationToken cancellationToken = default)
        {
            Attempts++;
            return Task.CompletedTask;
        }
    }

    public sealed class FailingConsumer : IConsumer<OrderMessage>
    {
        public int FailuresBeforeSuccess { get; set; }

        public int Attempts { get; private set; }

        public Task ConsumeAsync(IMessageContext<OrderMessage> messageContext, CancellationToken cancellationToken = default)
        {
            Attempts++;
            return Attempts <= FailuresBeforeSuccess
                ? throw new InvalidOperationException($"attempt {Attempts} failed")
                : Task.CompletedTask;
        }
    }

    public sealed class ExplodingPricingConsumer : IRequestConsumer<PricingRequest, PricingReply>
    {
        public int Attempts { get; private set; }

        public Task<PricingReply> ConsumeAsync(IMessageContext<PricingRequest> messageContext, CancellationToken cancellationToken = default)
        {
            Attempts++;
            throw new InvalidOperationException("pricing unavailable");
        }
    }
}
