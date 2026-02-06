using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Vulthil.Messaging.RabbitMq.Publishing;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class RabbitMqPublisherTests : BaseUnitTestCase
{
    private readonly Lazy<RabbitMqPublisher> _lazyTarget;
    private RabbitMqPublisher Target => _lazyTarget.Value;

    public RabbitMqPublisherTests()
    {
        var logger = new Mock<ILogger<RabbitMqPublisher>>().Object;
        var channelMock = new Mock<IChannel>();
        // Setup BasicPublishAsync with ReadOnlyMemory<byte> for the body parameter
        channelMock.Setup(x => x.BasicPublishAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var connectionMock = new Mock<IConnection>();
        connectionMock.Setup(x => x.CreateChannelAsync(
            It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channelMock.Object);

        Use(logger);
        Use(connectionMock.Object);
        _lazyTarget = new(CreateInstance<RabbitMqPublisher>);
    }

    [Fact]
    public async Task PublishAsyncWithValidMessagePublishesSuccessfully()
    {
        // Arrange
        var message = new TestMessage { Content = "test" };

        // Act
        await Target.PublishAsync(message, cancellationToken: CancellationToken);

        // Assert - If we get here without exception, the test passes
        Assert.True(true);
    }

    [Fact]
    public async Task PublishAsyncWithNullMessageThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => Target.PublishAsync<TestMessage>(null!, cancellationToken: CancellationToken));
    }

    private sealed class TestMessage
    {
        public string Content { get; set; } = string.Empty;
    }
}
