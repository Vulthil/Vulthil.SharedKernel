using System.Diagnostics;
using System.Text.Json;
using RabbitMQ.Client;
using Vulthil.Messaging.RabbitMq.HealthChecks;
using Vulthil.Messaging.RabbitMq.Publishing;
using Vulthil.Messaging.RabbitMq.Requests;
using Vulthil.Messaging.Transport;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class RabbitMqRequesterTests : BaseUnitTestCase
{
    private readonly Lazy<RabbitMqRequester> _lazyTarget;
    private readonly Mock<IInternalPublisher> _publisherMock;
    private readonly RabbitMqBusStartupStatus _startupStatus = new();

    private RabbitMqRequester Target => _lazyTarget.Value;

    public RabbitMqRequesterTests()
    {
        Use(TestProviders.Build());
        Use(_startupStatus);

        var channelMock = GetMock<IChannel>();
        channelMock
            .Setup(c => c.QueueDeclareAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueDeclareOk("callback.test", 0, 0));
        channelMock
            .Setup(c => c.BasicConsumeAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("consumer-tag");

        GetMock<IConnection>()
            .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        Use(CreateInstance<ResponseListener>());

        _publisherMock = GetMock<IInternalPublisher>();
        _publisherMock
            .Setup(p => p.InternalPublishAsync(
                It.IsAny<byte[]>(), It.IsAny<BasicProperties>(), It.IsAny<string>(), It.IsAny<MessageConfiguration>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _lazyTarget = new Lazy<RabbitMqRequester>(CreateInstance<RabbitMqRequester>);
    }

    [Fact]
    public async Task RequestAsyncReturnsTimeoutFailureWhenNoResponseArrivesWithinPerRequestTimeout()
    {
        // Arrange
        _startupStatus.MarkStarted();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await Target.RequestAsync<TimeoutRequest, TimeoutResponse>(
            new TimeoutRequest("ping"),
            context =>
            {
                context.SetTimeout(TimeSpan.FromMilliseconds(200));
                return ValueTask.CompletedTask;
            },
            CancellationToken);
        stopwatch.Stop();

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Messaging.Request.Timeout");
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RequestAsyncCorrelatesOnAFreshRequestIdDistinctFromBusinessCorrelationId()
    {
        // Arrange
        _startupStatus.MarkStarted();
        const string businessCorrelationId = "order-42";
        BasicProperties? capturedProps = null;
        byte[]? capturedBody = null;
        _publisherMock
            .Setup(p => p.InternalPublishAsync(
                It.IsAny<byte[]>(), It.IsAny<BasicProperties>(), It.IsAny<string>(), It.IsAny<MessageConfiguration>(), It.IsAny<CancellationToken>()))
            .Callback((byte[] body, BasicProperties props, string _, MessageConfiguration _, CancellationToken _) =>
            {
                capturedBody = body;
                capturedProps = props;
            })
            .Returns(Task.CompletedTask);

        // Act
        var result = await Target.RequestAsync<TimeoutRequest, TimeoutResponse>(
            new TimeoutRequest("ping"),
            context =>
            {
                context.SetCorrelationId(businessCorrelationId);
                context.SetTimeout(TimeSpan.FromMilliseconds(200));
                return ValueTask.CompletedTask;
            },
            CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        capturedProps.ShouldNotBeNull();
        capturedProps.CorrelationId.ShouldNotBe(businessCorrelationId);
        Guid.TryParse(capturedProps.CorrelationId, out _).ShouldBeTrue();

        capturedBody.ShouldNotBeNull();
        var envelope = JsonSerializer.Deserialize<MessageEnvelope>(capturedBody);
        envelope.ShouldNotBeNull();
        envelope.CorrelationId.ShouldBe(businessCorrelationId);
        envelope.RequestId.ShouldBe(capturedProps.CorrelationId);
    }

    [Fact]
    public async Task RequestAsyncHoldsThePublishUntilTheBusHasStarted()
    {
        // Arrange: the bus never finishes starting, so the request must time out without ever publishing —
        // otherwise it would be sent before the responder's queue and bindings exist and expire unanswered.

        // Act
        var result = await Target.RequestAsync<TimeoutRequest, TimeoutResponse>(
            new TimeoutRequest("ping"),
            context =>
            {
                context.SetTimeout(TimeSpan.FromMilliseconds(200));
                return ValueTask.CompletedTask;
            },
            CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Messaging.Request.Timeout");
        _publisherMock.Verify(p => p.InternalPublishAsync(
            It.IsAny<byte[]>(), It.IsAny<BasicProperties>(), It.IsAny<string>(), It.IsAny<MessageConfiguration>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RequestAsyncPublishesOnceTheBusBecomesReady()
    {
        // Arrange: readiness arrives while the request is already waiting.
        var pending = Target.RequestAsync<TimeoutRequest, TimeoutResponse>(
            new TimeoutRequest("ping"),
            context =>
            {
                context.SetTimeout(TimeSpan.FromSeconds(1));
                return ValueTask.CompletedTask;
            },
            CancellationToken);

        // Act
        _startupStatus.MarkStarted();
        var result = await pending;

        // Assert: the request was published and then timed out waiting for a response, proving the publish
        // happened after readiness rather than being dropped.
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Messaging.Request.Timeout");
        _publisherMock.Verify(p => p.InternalPublishAsync(
            It.IsAny<byte[]>(), It.IsAny<BasicProperties>(), It.IsAny<string>(), It.IsAny<MessageConfiguration>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private sealed record TimeoutRequest(string Value);

    private sealed record TimeoutResponse(string Value);
}
