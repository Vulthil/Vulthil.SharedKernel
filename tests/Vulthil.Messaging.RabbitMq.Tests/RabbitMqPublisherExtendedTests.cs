using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Vulthil.Messaging.RabbitMq.Publishing;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class RabbitMqPublisherExtendedTests : BaseUnitTestCase
{
    private readonly Lazy<RabbitMqPublisher> _lazyTarget;
    private readonly Mock<IChannel> _channelMock;

    private RabbitMqPublisher Target => _lazyTarget.Value;

    public RabbitMqPublisherExtendedTests()
    {
        var logger = GetMock<ILogger<RabbitMqPublisher>>().Object;
        _channelMock = GetMock<IChannel>();

        _channelMock.Setup(x => x.BasicPublishAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var connectionMock = GetMock<IConnection>();
        connectionMock.Setup(x => x.CreateChannelAsync(
            It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_channelMock.Object);

        Use(logger);
        Use(connectionMock.Object);
        Use(Options.Create(new MessagingOptions()));
        _lazyTarget = new(CreateInstance<RabbitMqPublisher>);
    }

    [Fact]
    public async Task PublishAsyncShouldSetMessageTypeInBasicProperties()
    {
        // Arrange
        var message = new TestMessage { Content = "test content" };
        BasicProperties? capturedProperties = null;

        _channelMock.Setup(x => x.BasicPublishAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Callback((string ex, string route, bool mandatory, BasicProperties props, ReadOnlyMemory<byte> body, CancellationToken ct) =>
            {
                capturedProperties = props;
            })
            .Returns(ValueTask.CompletedTask);

        // Act
        await Target.PublishAsync(message, cancellationToken: CancellationToken);

        // Assert
        capturedProperties.ShouldNotBeNull();
        capturedProperties.Type.ShouldBe(typeof(TestMessage).FullName);
    }

    [Fact]
    public async Task PublishAsyncWithMultipleMessagesPublishesEach()
    {
        // Arrange
        var messages = new[]
        {
            new TestMessage { Content = "message 1" },
            new TestMessage { Content = "message 2" },
            new TestMessage { Content = "message 3" }
        };

        // Act
        foreach (var message in messages)
        {
            await Target.PublishAsync(message, cancellationToken: CancellationToken);
        }

        // Assert
        _channelMock.Verify(
            x => x.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task PublishAsyncShouldPublishToCorrectExchange()
    {
        // Arrange
        var message = new TestMessage { Content = "test" };
        string? capturedExchange = null;

        _channelMock.Setup(x => x.BasicPublishAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Callback((string ex, string route, bool mandatory, BasicProperties props, ReadOnlyMemory<byte> body, CancellationToken ct) =>
            {
                capturedExchange = ex;
            })
            .Returns(ValueTask.CompletedTask);

        // Act
        await Target.PublishAsync(message, cancellationToken: CancellationToken);

        // Assert
        capturedExchange.ShouldBe(typeof(TestMessage).FullName);
    }

    private sealed class TestMessage
    {
        public string Content { get; set; } = string.Empty;
    }
}
