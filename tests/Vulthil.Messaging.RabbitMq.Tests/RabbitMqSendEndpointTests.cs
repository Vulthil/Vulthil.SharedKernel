using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Vulthil.Messaging.RabbitMq.Publishing;
using Vulthil.Messaging.RabbitMq.Sending;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class RabbitMqSendEndpointTests : BaseUnitTestCase
{
    private const string QueueName = "order-commands";
    private static readonly Uri Address = new($"queue:{QueueName}");

    private readonly Lazy<RabbitMqSendEndpoint> _lazyTarget;
    private readonly Mock<IInternalPublisher> _publisherMock;
    private readonly Mock<IMessageConfigurationProvider> _messageConfigurationProviderMock;

    private RabbitMqSendEndpoint Target => _lazyTarget.Value;

    public RabbitMqSendEndpointTests()
    {
        _publisherMock = GetMock<IInternalPublisher>();
        _publisherMock.Setup(p => p.InternalSendAsync(
            It.IsAny<byte[]>(), It.IsAny<BasicProperties>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _messageConfigurationProviderMock = GetMock<IMessageConfigurationProvider>();
        _messageConfigurationProviderMock.Setup(p => p.GetMessageConfiguration(It.IsAny<Type>()))
            .Returns<Type>(t => new MessageConfiguration(t.FullName!));
        _messageConfigurationProviderMock.SetupGet(p => p.JsonSerializerOptions)
            .Returns(new System.Text.Json.JsonSerializerOptions());

        var logger = GetMock<ILogger<RabbitMqSendEndpoint>>().Object;

        _lazyTarget = new(() => new RabbitMqSendEndpoint(
            Address,
            QueueName,
            _publisherMock.Object,
            _messageConfigurationProviderMock.Object,
            logger));
    }

    [Fact]
    public async Task SendAsyncShouldRouteToDestinationQueueByName()
    {
        // Arrange
        var message = new TestMessage { Content = "send-me" };
        string? capturedQueue = null;
        _publisherMock.Setup(p => p.InternalSendAsync(
                It.IsAny<byte[]>(), It.IsAny<BasicProperties>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((byte[] _, BasicProperties _, string queue, CancellationToken _) => capturedQueue = queue)
            .Returns(Task.CompletedTask);

        // Act
        await Target.SendAsync(message, CancellationToken);

        // Assert
        capturedQueue.ShouldBe(QueueName);
    }

    [Fact]
    public async Task SendAsyncShouldApplyCorrelationIdFormatter()
    {
        // Arrange
        var message = new TestMessage { Content = "abc" };
        BasicProperties? captured = null;
        _publisherMock.Setup(p => p.InternalSendAsync(
                It.IsAny<byte[]>(), It.IsAny<BasicProperties>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((byte[] _, BasicProperties props, string _, CancellationToken _) => captured = props)
            .Returns(Task.CompletedTask);

        _messageConfigurationProviderMock.Setup(p => p.GetMessageConfiguration(It.IsAny<Type>()))
            .Returns<Type>(t => new MessageConfiguration(t.FullName!)
            {
                CorrelationIdFormatter = m => "correlation-from-formatter"
            });

        // Act
        await Target.SendAsync(message, CancellationToken);

        // Assert
        captured.ShouldNotBeNull();
        captured.CorrelationId.ShouldBe("correlation-from-formatter");
    }

    [Fact]
    public async Task SendAsyncShouldPreferExplicitCorrelationIdOverFormatter()
    {
        // Arrange
        var message = new TestMessage { Content = "abc" };
        BasicProperties? captured = null;
        _publisherMock.Setup(p => p.InternalSendAsync(
                It.IsAny<byte[]>(), It.IsAny<BasicProperties>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((byte[] _, BasicProperties props, string _, CancellationToken _) => captured = props)
            .Returns(Task.CompletedTask);

        _messageConfigurationProviderMock.Setup(p => p.GetMessageConfiguration(It.IsAny<Type>()))
            .Returns<Type>(t => new MessageConfiguration(t.FullName!)
            {
                CorrelationIdFormatter = m => "from-formatter"
            });

        // Act
        await Target.SendAsync(
            message,
            ctx =>
            {
                ctx.SetCorrelationId("explicit");
                return ValueTask.CompletedTask;
            },
            CancellationToken);

        // Assert
        captured.ShouldNotBeNull();
        captured.CorrelationId.ShouldBe("explicit");
    }

    [Fact]
    public async Task SendAsyncWithNullMessageThrows()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => Target.SendAsync<TestMessage>(null!, CancellationToken));
    }

    [Fact]
    public async Task SendAsyncShouldPopulateBasicPropertiesType()
    {
        // Arrange
        var message = new TestMessage { Content = "x" };
        BasicProperties? captured = null;
        _publisherMock.Setup(p => p.InternalSendAsync(
                It.IsAny<byte[]>(), It.IsAny<BasicProperties>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((byte[] _, BasicProperties props, string _, CancellationToken _) => captured = props)
            .Returns(Task.CompletedTask);

        // Act
        await Target.SendAsync(message, CancellationToken);

        // Assert
        captured.ShouldNotBeNull();
        var expectedUrn = new MessageConfiguration(typeof(TestMessage).FullName!).Urn.AbsoluteUri;
        captured.Type.ShouldBe(expectedUrn);
        captured.Persistent.ShouldBeTrue();
    }

    private sealed class TestMessage
    {
        public string Content { get; set; } = string.Empty;
    }
}
