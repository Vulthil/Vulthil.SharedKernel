using System.Text;
using System.Text.Json;
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
    private const string QueueName = "orders";
    private const string ReplyQueue = "reply.queue";
    private const string RequestCorrelationId = "corr-1";
    private const int ConsumerFailedEventId = 2202;

    private readonly Mock<IChannel> _channel;
    private readonly List<CapturedPublish> _publishes = [];

    private IAsyncBasicConsumer? _capturedConsumer;

    public RabbitMqConsumerWorkerTests()
    {
        Use(TestProviders.Build());
        Use<IEnumerable<IConsumeFilter<TestMessage>>>([]);
        Use<IEnumerable<IConsumeFilter<TestRequest>>>([]);
        Use(new DeliveryDispatcher(new AutoMockerServiceScopeFactory(AutoMocker), NullLogger<DeliveryDispatcher>.Instance));
        Use<ILogger<RabbitMqConsumerWorker>>(NullLogger<RabbitMqConsumerWorker>.Instance);
        Use<TimeProvider>(new FakeTimeProvider());
        Use(0);

        _channel = GetMock<IChannel>();
        _channel
            .Setup(c => c.BasicConsumeAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
            .Callback((string _, bool _, string _, bool _, bool _, IDictionary<string, object?> _, IAsyncBasicConsumer consumer, CancellationToken _) =>
                _capturedConsumer = consumer)
            .ReturnsAsync("consumer-tag");
        _channel
            .Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Callback((string exchange, string routingKey, bool _, BasicProperties props, ReadOnlyMemory<byte> body, CancellationToken _) =>
                _publishes.Add(new CapturedPublish(exchange, routingKey, props, body.ToArray())))
            .Returns(ValueTask.CompletedTask);
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
    public void WithRetryCountLeavesTheOriginalDeliveryHeadersUntouched()
    {
        // Arrange
        var headers = new Dictionary<string, object?>();
        var delivery = new BasicDeliverEventArgs(
            consumerTag: "consumer-tag",
            deliveryTag: 7,
            redelivered: false,
            exchange: "exchange",
            routingKey: "rk",
            new BasicProperties { Headers = headers },
            ReadOnlyMemory<byte>.Empty);

        // Act
        var retried = RabbitMqConsumerWorker.WithRetryCount(delivery, 3);

        // Assert
        headers.ShouldBeEmpty();
        retried.BasicProperties.Headers.ShouldNotBeSameAs(headers);
    }

    [Fact]
    public async Task ProcessAsyncLeavesTheDeliveryUnsettledWhenCancelledDuringShutdown()
    {
        // Arrange
        Use(new CancellingConsumer());
        await StartWorkerAsync(QueueConsuming<CancellingConsumer, TestMessage>());
        using var cancelledSource = new CancellationTokenSource();
        await cancelledSource.CancelAsync();

        // Act
        await DeliverAsync(new TestMessage("payload"), cancelledSource.Token);

        // Assert
        VerifyUnsettled();
        _publishes.ShouldBeEmpty();
    }

    [Fact]
    public async Task ARequestConsumerStoppedByShutdownSendsNoReplyAndLeavesTheDeliveryUnsettled()
    {
        // Arrange
        Use(new CancellingRequestConsumer());
        await StartWorkerAsync(QueueAnswering<CancellingRequestConsumer>());
        using var cancelledSource = new CancellationTokenSource();
        await cancelledSource.CancelAsync();

        // Act
        await DeliverAsync(new TestRequest("query"), cancelledSource.Token, ReplyQueue, RequestCorrelationId);

        // Assert
        VerifyUnsettled();
        _publishes.ShouldBeEmpty();
    }

    [Fact]
    public async Task ARequestConsumersReplyGoesToItsReplyToQueueThroughTheDefaultExchangeUnderTheRequestsCorrelationId()
    {
        // Arrange
        Use(new EchoRequestConsumer());
        await StartWorkerAsync(QueueAnswering<EchoRequestConsumer>());

        // Act
        await DeliverAsync(new TestRequest("Find users"), CancellationToken, ReplyQueue, RequestCorrelationId);

        // Assert
        var reply = _publishes.ShouldHaveSingleItem();
        reply.Exchange.ShouldBe(string.Empty);
        reply.RoutingKey.ShouldBe(ReplyQueue);
        reply.Properties.CorrelationId.ShouldBe(RequestCorrelationId);
        var envelope = JsonSerializer.Deserialize<MessageEnvelope>(reply.Body).ShouldNotBeNull();
        envelope.RequestId.ShouldBe(RequestCorrelationId);
        envelope.Message.Deserialize<TestResponse>().ShouldNotBeNull().Result.ShouldBe("Processed: Find users");
        _channel.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AConsumerFailureIsLoggedWithTheQueueTheRoutingKeyAndTheMessageTypeInItsScope()
    {
        // Arrange
        using var logs = new ScopeCapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs));
        Use(new DeliveryDispatcher(new AutoMockerServiceScopeFactory(AutoMocker), loggerFactory.CreateLogger<DeliveryDispatcher>()));
        Use(loggerFactory.CreateLogger<RabbitMqConsumerWorker>());
        Use(new ThrowingConsumer());
        await StartWorkerAsync(QueueConsuming<ThrowingConsumer, TestMessage>());

        // Act
        await DeliverAsync(new TestMessage("payload"), CancellationToken);

        // Assert
        var failure = logs.Entries.Where(entry => entry.EventId == ConsumerFailedEventId).ShouldHaveSingleItem();
        failure.Scope["Queue"].ShouldBe(QueueName);
        failure.Scope["RoutingKey"].ShouldBe(QueueName);
        failure.Scope["MessageType"].ShouldBe(typeof(TestMessage).FullName);
    }

    private static QueueDefinition QueueConsuming<TConsumer, TMessage>()
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : notnull
    {
        var queue = new QueueDefinition(QueueName);
        queue.AddConsumer(new ConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(TConsumer)),
            MessageType = new MessageType(typeof(TMessage)),
        });
        return queue;
    }

    private static QueueDefinition QueueAnswering<TConsumer>()
        where TConsumer : class, IRequestConsumer<TestRequest, TestResponse>
    {
        var queue = new QueueDefinition(QueueName);
        queue.AddConsumer(new RequestConsumerRegistration
        {
            ConsumerType = new ConsumerType(typeof(TConsumer)),
            MessageType = new MessageType(typeof(TestRequest)),
            ResponseType = typeof(TestResponse),
        });
        return queue;
    }

    private async Task StartWorkerAsync(QueueDefinition queue)
    {
        Use(queue);
        Use(CreateInstance<QueueDispatchPlans>());

        var worker = CreateInstance<RabbitMqConsumerWorker>();
        await worker.StartAsync(CancellationToken);
        _capturedConsumer.ShouldNotBeNull();
    }

    private Task DeliverAsync<TMessage>(TMessage message, CancellationToken cancellationToken, string? replyTo = null, string? correlationId = null)
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
                ReplyTo = replyTo,
                CorrelationId = correlationId,
                Headers = new Dictionary<string, object?>(),
            },
            JsonSerializer.SerializeToUtf8Bytes(message),
            cancellationToken);

    private void VerifyUnsettled()
    {
        _channel.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        _channel.Verify(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed record CapturedPublish(string Exchange, string RoutingKey, BasicProperties Properties, byte[] Body);

    private sealed record TestMessage(string Value);

    internal sealed record TestRequest(string Query);

    internal sealed record TestResponse(string Result);

    private sealed class CancellingConsumer : IConsumer<TestMessage>
    {
        public Task ConsumeAsync(IMessageContext<TestMessage> messageContext, CancellationToken cancellationToken = default)
            => throw new OperationCanceledException();
    }

    private sealed class ThrowingConsumer : IConsumer<TestMessage>
    {
        public Task ConsumeAsync(IMessageContext<TestMessage> messageContext, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("consumer exploded");
    }

    private sealed class CancellingRequestConsumer : IRequestConsumer<TestRequest, TestResponse>
    {
        public Task<TestResponse> ConsumeAsync(IMessageContext<TestRequest> messageContext, CancellationToken cancellationToken = default)
            => throw new OperationCanceledException(cancellationToken);
    }

    private sealed class EchoRequestConsumer : IRequestConsumer<TestRequest, TestResponse>
    {
        public Task<TestResponse> ConsumeAsync(IMessageContext<TestRequest> messageContext, CancellationToken cancellationToken = default)
            => Task.FromResult(new TestResponse($"Processed: {messageContext.Message.Query}"));
    }

    private sealed record CapturedLog(int EventId, IReadOnlyDictionary<string, object?> Scope);

    /// <summary>
    /// Captures each entry with the values of every scope open when it was logged, read from the logger factory's
    /// shared scope provider, so a test sees scopes opened on one logger in entries written by another.
    /// </summary>
    private sealed class ScopeCapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly List<CapturedLog> _entries = [];
        private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

        public IReadOnlyList<CapturedLog> Entries => _entries;

        public ILogger CreateLogger(string categoryName) => new ScopeCapturingLogger(this);

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

        public void Dispose()
        {
        }

        private sealed class ScopeCapturingLogger(ScopeCapturingLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
                => provider._scopes.Push(state);

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var scope = new Dictionary<string, object?>();
                provider._scopes.ForEachScope(static (scopeState, values) =>
                {
                    if (scopeState is IEnumerable<KeyValuePair<string, object?>> pairs)
                    {
                        foreach (var (key, value) in pairs)
                        {
                            values[key] = value;
                        }
                    }
                }, scope);
                provider._entries.Add(new CapturedLog(eventId.Id, scope));
            }
        }
    }
}
