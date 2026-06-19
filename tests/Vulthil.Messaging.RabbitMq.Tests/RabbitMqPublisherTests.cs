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
    private readonly Mock<IMessageConfigurationProvider> _messageConfigurationProviderMock;

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
        _messageConfigurationProviderMock = GetMock<IMessageConfigurationProvider>();
        _messageConfigurationProviderMock.Setup(x => x.GetMessageConfiguration(It.IsAny<Type>()))
            .Returns<Type>(t => new MessageConfiguration(t.FullName!));

        Use(logger);
        Use(Options.Create(new RabbitMqTransportOptions { PublishChannelPoolSize = 1 }));
        UseReal<RabbitMqChannelPool>();
        _lazyTarget = new(CreateInstance<RabbitMqPublisher>);
    }

    [Fact]
    public async Task PublishAsyncWithValidMessagePublishesSuccessfully()
    {
        // Arrange
        var message = new TestMessage { Content = "test" };

        // Act
        await Target.PublishAsync(message, cancellationToken: CancellationToken);

        // Assert — publish is pub/sub, so it is not mandatory (zero subscribers is normal).
        _channelMock.Verify(x => x.BasicPublishAsync(
            typeof(TestMessage).FullName!,
            string.Empty,
            false,
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

    [Fact]
    public async Task SendKeepsAStillOpenChannelWhenThePublishThrows()
    {
        // Arrange — an unroutable mandatory send throws, but the channel stays open (a return/nack does not fault it).
        _channelMock.Setup(x => x.BasicPublishAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("returned"));
        _channelMock.SetupGet(channel => channel.IsOpen).Returns(true);

        // Act
        await Should.ThrowAsync<InvalidOperationException>(
            () => Target.InternalSendAsync([], new BasicProperties(), "some-queue", CancellationToken));

        // Assert — a still-open channel is returned to the pool, not disposed.
        _channelMock.Verify(channel => channel.DisposeAsync(), Times.Never);
    }

    [Fact]
    public async Task SendDiscardsAChannelThatFaultedClosed()
    {
        // Arrange — the publish throws and the channel is closed (a genuine fault).
        _channelMock.Setup(x => x.BasicPublishAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("faulted"));
        _channelMock.SetupGet(channel => channel.IsOpen).Returns(false);

        // Act
        await Should.ThrowAsync<InvalidOperationException>(
            () => Target.InternalSendAsync([], new BasicProperties(), "some-queue", CancellationToken));

        // Assert — a faulted (closed) channel is discarded.
        _channelMock.Verify(channel => channel.DisposeAsync(), Times.Once);
    }

    private sealed class TestMessage
    {
        public string Content { get; set; } = string.Empty;
    }
}
