using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Vulthil.Messaging.RabbitMq.Publishing;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class RabbitMqPublisherTests : BaseUnitTestCase
{
    private readonly Lazy<RabbitMqPublisher> _lazyTarget;
    private readonly Mock<IChannel> _channelMock;

    private RabbitMqPublisher Target => _lazyTarget.Value;

    public RabbitMqPublisherTests()
    {
        var logger = GetMock<ILogger<RabbitMqPublisher>>().Object;
        _channelMock = GetMock<IChannel>();
        // Setup BasicPublishAsync with ReadOnlyMemory<byte> for the body parameter
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
    public async Task PublishAsyncWithValidMessagePublishesSuccessfully()
    {
        // Arrange
        var message = new TestMessage { Content = "test" };

        // Act
        await Target.PublishAsync(message, cancellationToken: CancellationToken);

        // Assert
        _channelMock.Verify(x => x.BasicPublishAsync(
            typeof(TestMessage).FullName!,
            string.Empty,
            true,
            It.IsAny<BasicProperties>(),
            It.IsAny<ReadOnlyMemory<byte>>(),
            CancellationToken), Times.Once);
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
