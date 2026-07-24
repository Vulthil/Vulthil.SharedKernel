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

public sealed class RabbitMqBusStartupRetryTests : BaseUnitTestCase
{
    private readonly Mock<IChannel> _channel = new();
    private readonly Lazy<RabbitMqBus> _lazyTarget;
    private IAsyncBasicConsumer? _capturedConsumer;

    private RabbitMqBus Target => _lazyTarget.Value;

    public RabbitMqBusStartupRetryTests()
    {
        _channel
            .Setup(c => c.BasicConsumeAsync(
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object?>>(), It.IsAny<IAsyncBasicConsumer>(), It.IsAny<CancellationToken>()))
            .Callback((string _, bool _, string _, bool _, bool _, IDictionary<string, object?> _, IAsyncBasicConsumer consumer, CancellationToken _) =>
                _capturedConsumer = consumer)
            .ReturnsAsync("consumer-tag");

        Use(new RabbitMqBusStartupStatus());
        Use<ILoggerFactory>(NullLoggerFactory.Instance);
        Use<ILogger<RabbitMqBus>>(NullLogger<RabbitMqBus>.Instance);
        Use<IServiceScopeFactory>(new AutoMockerServiceScopeFactory(AutoMocker));

        _lazyTarget = new(CreateInstance<RabbitMqBus>);
    }

    protected override ValueTask Dispose() => _lazyTarget.IsValueCreated ? Target.DisposeAsync() : base.Dispose();

    [Fact]
    public async Task StartAsyncFailThenRetrySucceedsWithoutDoublingHandlersOrRejectingTheRpcConsumer()
    {
        // Arrange
        var handlerHits = 0;
        Use(new RecordingConsumer(() => handlerHits++));
        Use<IEnumerable<IConsumeFilter<TestMessage>>>([]);

        var provider = TestProviders.Build(cfg => cfg.ConfigureQueue("orders", queue =>
        {
            queue.AddConsumer<RecordingConsumer>();
            queue.AddRequestConsumer<RecordingRequestConsumer>();
        }));
        Use(provider);

        GetMock<IConnection>()
            .SetupSequence(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_channel.Object)
            .ThrowsAsync(new InvalidOperationException("connection blip"))
            .ReturnsAsync(_channel.Object)
            .ReturnsAsync(_channel.Object);

        // Act
        await Should.ThrowAsync<InvalidOperationException>(() => Target.StartAsync(CancellationToken));
        await Target.StartAsync(CancellationToken);

        _capturedConsumer.ShouldNotBeNull();
        await _capturedConsumer.HandleBasicDeliverAsync(
            "consumer-tag",
            1,
            false,
            "orders",
            "orders",
            new BasicProperties { Type = typeof(TestMessage).FullName, Headers = new Dictionary<string, object?>() },
            JsonSerializer.SerializeToUtf8Bytes(new TestMessage("payload")),
            CancellationToken.None);

        // Assert
        handlerHits.ShouldBe(1);
    }

    internal sealed record TestMessage(string Value);
    internal sealed record TestRequest(string Query);
    internal sealed record TestResponse(string Result);

    private sealed class RecordingConsumer(Action onConsume) : IConsumer<TestMessage>
    {
        public Task ConsumeAsync(IMessageContext<TestMessage> messageContext, CancellationToken cancellationToken = default)
        {
            onConsume();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRequestConsumer : IRequestConsumer<TestRequest, TestResponse>
    {
        public Task<TestResponse> ConsumeAsync(IMessageContext<TestRequest> messageContext, CancellationToken cancellationToken = default)
            => Task.FromResult(new TestResponse($"Processed: {messageContext.Message.Query}"));
    }
}
