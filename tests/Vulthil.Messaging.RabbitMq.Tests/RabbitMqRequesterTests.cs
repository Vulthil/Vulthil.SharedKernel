using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.RabbitMq.HealthChecks;
using Vulthil.Messaging.RabbitMq.Publishing;
using Vulthil.Messaging.RabbitMq.Requests;
using Vulthil.Messaging.Transport;
using Vulthil.Results;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class RabbitMqRequesterTests : BaseUnitTestCase
{
    private const string ReplyQueue = "callback.test";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private readonly Lazy<RabbitMqRequester> _lazyTarget;
    private readonly RabbitMqBusStartupStatus _startupStatus = new();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly IMessageConfigurationProvider _provider = TestProviders.Build();
    private readonly List<CapturedRequest> _published = [];
    private readonly TaskCompletionSource _firstPublish = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private IAsyncBasicConsumer? _replyConsumer;

    private RabbitMqRequester Target => _lazyTarget.Value;

    public RabbitMqRequesterTests()
    {
        Use(_provider);
        Use(_startupStatus);
        Use<TimeProvider>(_timeProvider);

        var channelMock = GetMock<IChannel>();
        channelMock
            .Setup(c => c.QueueDeclareAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk(ReplyQueue, 0, 0));
        channelMock
            .Setup(c => c.BasicConsumeAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
            .Callback((string _, bool _, string _, bool _, bool _, IDictionary<string, object?> _, IAsyncBasicConsumer consumer, CancellationToken _) =>
                _replyConsumer = consumer)
            .ReturnsAsync("consumer-tag");

        GetMock<IConnection>()
            .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        Use(CreateInstance<ResponseListener>());

        GetMock<IInternalPublisher>()
            .Setup(p => p.InternalPublishAsync(
                It.IsAny<byte[]>(), It.IsAny<BasicProperties>(), It.IsAny<string>(), It.IsAny<MessageConfiguration>(), It.IsAny<CancellationToken>()))
            .Callback((byte[] body, BasicProperties props, string _, MessageConfiguration _, CancellationToken _) =>
            {
                _published.Add(new CapturedRequest(props, body));
                _firstPublish.TrySetResult();
            })
            .Returns(Task.CompletedTask);

        _lazyTarget = new Lazy<RabbitMqRequester>(CreateInstance<RabbitMqRequester>);
    }

    private Task<Result<TimeoutResponse>> SendRequestAsync(Action<IRequestContext>? configure = null)
        => Target.RequestAsync<TimeoutRequest, TimeoutResponse>(
            new TimeoutRequest("ping"),
            context =>
            {
                context.SetTimeout(RequestTimeout);
                configure?.Invoke(context);
                return ValueTask.CompletedTask;
            },
            CancellationToken);

    private Task DeliverReplyAsync(string requestId, TimeoutResponse reply)
    {
        var envelope = new MessageEnvelope
        {
            RequestId = requestId,
            MessageType = _provider.GetUrn(typeof(TimeoutResponse)),
            Message = JsonSerializer.SerializeToElement(reply, _provider.JsonSerializerOptions),
        };

        return _replyConsumer!.HandleBasicDeliverAsync(
            "consumer-tag",
            1,
            false,
            string.Empty,
            ReplyQueue,
            new BasicProperties { CorrelationId = requestId },
            JsonSerializer.SerializeToUtf8Bytes(envelope, _provider.JsonSerializerOptions),
            CancellationToken);
    }

    [Fact]
    public async Task RequestAsyncReturnsTimeoutFailureWhenNoResponseArrivesWithinPerRequestTimeout()
    {
        // Arrange
        _startupStatus.MarkStarted();

        // Act
        var pending = SendRequestAsync();
        _timeProvider.Advance(RequestTimeout - TimeSpan.FromSeconds(1));
        var settledBeforeTimeout = pending.IsCompleted;
        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        // Assert
        settledBeforeTimeout.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Messaging.Request.Timeout");
        result.Error.Description.ShouldBe("Request timed out after 30s");
    }

    [Fact]
    public async Task RequestAsyncCompletesWithTheReplyCorrelatedOnTheRequestId()
    {
        // Arrange
        _startupStatus.MarkStarted();
        var reply = new TimeoutResponse("pong");

        // Act
        var pending = SendRequestAsync();
        var requestId = _published.Single().Properties.CorrelationId!;
        await DeliverReplyAsync(requestId, reply);
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(reply);
    }

    [Fact]
    public async Task RequestAsyncCorrelatesOnAFreshRequestIdDistinctFromBusinessCorrelationId()
    {
        // Arrange
        _startupStatus.MarkStarted();
        const string businessCorrelationId = "order-42";

        // Act
        var pending = SendRequestAsync(context => context.SetCorrelationId(businessCorrelationId));
        _timeProvider.Advance(RequestTimeout);
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        var request = _published.ShouldHaveSingleItem();
        request.Properties.CorrelationId.ShouldNotBe(businessCorrelationId);
        Guid.TryParse(request.Properties.CorrelationId, out _).ShouldBeTrue();

        var envelope = JsonSerializer.Deserialize<MessageEnvelope>(request.Body);
        envelope.ShouldNotBeNull();
        envelope.CorrelationId.ShouldBe(businessCorrelationId);
        envelope.RequestId.ShouldBe(request.Properties.CorrelationId);
    }

    [Fact]
    public async Task RequestAsyncHoldsThePublishUntilTheBusHasStarted()
    {
        // Arrange: the bus never finishes starting, so the request must time out without ever publishing —
        // otherwise it would be sent before the responder's queue and bindings exist and expire unanswered.

        // Act
        var pending = SendRequestAsync();
        _timeProvider.Advance(RequestTimeout);
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Messaging.Request.Timeout");
        result.Error.Description.ShouldContain("waiting for the transport to start");
        _published.ShouldBeEmpty();
    }

    [Fact]
    public async Task RequestAsyncPublishesOnceTheBusBecomesReady()
    {
        // Arrange: readiness arrives while the request is already waiting.
        var pending = SendRequestAsync();
        var publishedBeforeReady = _published.Count;

        // Act
        _startupStatus.MarkStarted();
        await _firstPublish.Task.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        _timeProvider.Advance(RequestTimeout);
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);

        // Assert: the request was published and then timed out waiting for a response, proving the publish
        // happened after readiness rather than being dropped.
        publishedBeforeReady.ShouldBe(0);
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Messaging.Request.Timeout");
        _published.ShouldHaveSingleItem();
    }

    private sealed record CapturedRequest(BasicProperties Properties, byte[] Body);

    private sealed record TimeoutRequest(string Value);

    private sealed record TimeoutResponse(string Value);
}
