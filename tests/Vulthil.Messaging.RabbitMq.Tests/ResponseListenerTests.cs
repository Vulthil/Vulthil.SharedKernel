using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.RabbitMq.Requests;
using Vulthil.Messaging.Transport;
using Vulthil.Results;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class ResponseListenerTests : BaseUnitTestCase
{
    private const int ResponseWaiterNotFoundEventId = 1401;
    private const string DeclaredReplyQueue = "callback.test";
    private const ulong DeliveryTag = 7;

    private static readonly Uri _responseUrn = new("urn:message:Vulthil.Messaging.RabbitMq.Tests:PricingReply");
    private static readonly PricingReply _reply = new("sku-1", 9.5m);

    private readonly Lazy<ResponseListener> _lazyTarget;
    private readonly Mock<IChannel> _channel = new();
    private readonly CapturingLogger _logger = new();
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly List<QueueDeclaration> _declaredQueues = [];
    private readonly List<Consumption> _consumptions = [];

    private IAsyncBasicConsumer? _replyConsumer;

    private ResponseListener Target => _lazyTarget.Value;

    public ResponseListenerTests()
    {
        var provider = TestProviders.Build();
        _jsonOptions = provider.JsonSerializerOptions;
        Use(provider);
        Use<ILogger<ResponseListener>>(_logger);

        _channel
            .Setup(c => c.QueueDeclareAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback((string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object?> _, bool _, bool _, CancellationToken _) =>
                _declaredQueues.Add(new QueueDeclaration(queue, durable, exclusive, autoDelete)))
            .ReturnsAsync(new QueueDeclareOk(DeclaredReplyQueue, 0, 0));
        _channel
            .Setup(c => c.BasicConsumeAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
            .Callback((string queue, bool autoAck, string _, bool _, bool _, IDictionary<string, object?> _, IAsyncBasicConsumer consumer, CancellationToken _) =>
            {
                _consumptions.Add(new Consumption(queue, autoAck));
                _replyConsumer = consumer;
            })
            .ReturnsAsync("consumer-tag");

        GetMock<IConnection>()
            .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_channel.Object);

        _lazyTarget = new(CreateInstance<ResponseListener>);
    }

    private byte[] Reply(Uri messageType, object message)
        => JsonSerializer.SerializeToUtf8Bytes(
            new MessageEnvelope
            {
                MessageType = messageType,
                Message = JsonSerializer.SerializeToElement(message, _jsonOptions),
            },
            _jsonOptions);

    private Task DeliverReplyAsync(string? correlationId)
        => _replyConsumer!.HandleBasicDeliverAsync(
            "consumer-tag",
            DeliveryTag,
            false,
            string.Empty,
            DeclaredReplyQueue,
            new BasicProperties { CorrelationId = correlationId },
            Reply(_responseUrn, _reply),
            CancellationToken);

    private async Task<string> GetReplyToQueueNameAsync() => await Target.GetReplyToQueueNameAsync(CancellationToken);

    private void VerifyAckedOnce()
        => _channel.Verify(c => c.BasicAckAsync(DeliveryTag, false, It.IsAny<CancellationToken>()), Times.Once);

    [Fact]
    public async Task FirstUseDeclaresAnExclusiveAutoDeleteCallbackQueueAndConsumesItWithManualAcks()
    {
        // Act
        var replyQueue = await Target.GetReplyToQueueNameAsync(CancellationToken);

        // Assert
        replyQueue.ShouldBe(DeclaredReplyQueue);
        var declaration = _declaredQueues.ShouldHaveSingleItem();
        declaration.Queue.ShouldStartWith("callback.");
        declaration.Durable.ShouldBeFalse();
        declaration.Exclusive.ShouldBeTrue();
        declaration.AutoDelete.ShouldBeTrue();
        var consumption = _consumptions.ShouldHaveSingleItem();
        consumption.Queue.ShouldBe(DeclaredReplyQueue);
        consumption.AutoAck.ShouldBeFalse();
    }

    [Fact]
    public async Task LaterUsesReuseTheReplyQueueDeclaredOnFirstUse()
    {
        // Arrange
        var first = await Target.GetReplyToQueueNameAsync(CancellationToken);

        // Act
        var second = await Target.GetReplyToQueueNameAsync(CancellationToken);

        // Assert
        second.ShouldBe(first);
        _declaredQueues.Count.ShouldBe(1);
        GetMock<IConnection>().Verify(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConcurrentFirstUsesDeclareTheReplyQueueOnce()
    {
        // Arrange
        var channelReady = new TaskCompletionSource<IChannel>(TaskCreationOptions.RunContinuationsAsynchronously);
        GetMock<IConnection>()
            .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .Returns(channelReady.Task);

        // Act
        var first = GetReplyToQueueNameAsync();
        var second = GetReplyToQueueNameAsync();
        channelReady.SetResult(_channel.Object);
        var names = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        // Assert
        names.ShouldAllBe(name => name == DeclaredReplyQueue);
        _declaredQueues.Count.ShouldBe(1);
        GetMock<IConnection>().Verify(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AReplyWithAKnownCorrelationIdCompletesItsWaiterAndIsAcked()
    {
        // Arrange
        var completion = new TaskCompletionSource<Result<PricingReply>>();
        await Target.GetReplyToQueueNameAsync(CancellationToken);
        Target.RegisterWaiter("req-1", completion, _responseUrn);

        // Act
        await DeliverReplyAsync("req-1");
        var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(_reply);
        VerifyAckedOnce();
    }

    [Fact]
    public async Task AReplyWithNoMatchingWaiterIsLoggedAndAcked()
    {
        // Arrange
        await Target.GetReplyToQueueNameAsync(CancellationToken);

        // Act
        await DeliverReplyAsync("nobody");

        // Assert
        _logger.EventIds.ShouldContain(eventId => eventId.Id == ResponseWaiterNotFoundEventId);
        VerifyAckedOnce();
    }

    [Fact]
    public async Task AReplyWithoutACorrelationIdIsAckedWithoutLogging()
    {
        // Arrange
        await Target.GetReplyToQueueNameAsync(CancellationToken);

        // Act
        await DeliverReplyAsync(correlationId: null);

        // Assert
        _logger.EventIds.ShouldNotContain(eventId => eventId.Id == ResponseWaiterNotFoundEventId);
        VerifyAckedOnce();
    }

    [Fact]
    public async Task AReplyForARemovedWaiterIsIgnoredAndAcked()
    {
        // Arrange
        var completion = new TaskCompletionSource<Result<PricingReply>>();
        await Target.GetReplyToQueueNameAsync(CancellationToken);
        Target.RegisterWaiter("req-1", completion, _responseUrn);
        Target.RemoveWaiter("req-1");

        // Act
        await DeliverReplyAsync("req-1");

        // Assert
        completion.Task.IsCompleted.ShouldBeFalse();
        _logger.EventIds.ShouldContain(eventId => eventId.Id == ResponseWaiterNotFoundEventId);
        VerifyAckedOnce();
    }

    [Fact]
    public async Task DisposeAsyncClosesTheReplyChannel()
    {
        // Arrange
        await Target.GetReplyToQueueNameAsync(CancellationToken);

        // Act
        await Target.DisposeAsync();

        // Assert
        _channel.Verify(c => c.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsyncBeforeFirstUseNeverOpensAChannel()
    {
        // Act
        await Target.DisposeAsync();

        // Assert
        GetMock<IConnection>().Verify(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed record QueueDeclaration(string Queue, bool Durable, bool Exclusive, bool AutoDelete);

    private sealed record Consumption(string Queue, bool AutoAck);

    public sealed record PricingReply(string Sku, decimal Price);

    private sealed class CapturingLogger : ILogger<ResponseListener>
    {
        public List<EventId> EventIds { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => EventIds.Add(eventId);
    }
}
