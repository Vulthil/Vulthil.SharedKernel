using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Vulthil.Messaging.RabbitMq.Publishing;
using Vulthil.xUnit;

namespace Vulthil.Messaging.RabbitMq.Tests;

public sealed class RabbitMqChannelPoolTests : BaseUnitTestCase
{
    private readonly Mock<IChannel> _channelMock;
    private readonly Mock<IConnection> _connectionMock;

    private readonly Lazy<RabbitMqChannelPool> _lazyTarget;
    private RabbitMqChannelPool Target => _lazyTarget.Value;

    public RabbitMqChannelPoolTests()
    {
        _channelMock = GetMock<IChannel>();
        _connectionMock = GetMock<IConnection>();
        _connectionMock
            .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_channelMock.Object);
        Use(Options.Create(new RabbitMqTransportOptions { PublishChannelPoolSize = 10 }));

        _lazyTarget = new(CreateInstance<RabbitMqChannelPool>);
    }

    protected override ValueTask Dispose() => _lazyTarget.IsValueCreated ? Target.DisposeAsync() : base.Dispose();


    [Fact]
    public async Task LeaseCreatesAChannelFromTheConnection()
    {
        // Act
        var leased = await Target.LeaseAsync(CancellationToken);

        // Assert
        leased.ShouldBeSameAs(_channelMock.Object);
        _connectionMock.Verify(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReturnedChannelIsReusedRatherThanRecreated()
    {
        // Arrange
        Use(Options.Create(new RabbitMqTransportOptions { PublishChannelPoolSize = 2 }));

        // Act
        var first = await Target.LeaseAsync(CancellationToken);
        Target.Return(first);
        var second = await Target.LeaseAsync(CancellationToken);

        // Assert
        second.ShouldBeSameAs(first);
        _connectionMock.Verify(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LeaseBeyondCapacityWaitsUntilAChannelIsReturned()
    {
        // Arrange
        Use(Options.Create(new RabbitMqTransportOptions { PublishChannelPoolSize = 1 }));
        var first = await Target.LeaseAsync(CancellationToken);

        // Act — a second lease cannot complete while the single slot is held.
        var secondLease = Target.LeaseAsync(CancellationToken).AsTask();
        secondLease.IsCompleted.ShouldBeFalse();

        Target.Return(first);

        // Assert — returning the channel frees the slot and unblocks the waiter.
        var second = await secondLease.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken);
        second.ShouldBeSameAs(first);
    }

    [Fact]
    public async Task DiscardDisposesTheChannelAndFreesTheSlot()
    {
        // Arrange
        Use(Options.Create(new RabbitMqTransportOptions { PublishChannelPoolSize = 1 }));
        var replacement = new Mock<IChannel>().Object;
        var channels = new Queue<IChannel>([_channelMock.Object, replacement]);
        _connectionMock
            .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(channels.Dequeue);

        // Act
        var leased = await Target.LeaseAsync(CancellationToken);
        await Target.DiscardAsync(leased);
        var next = await Target.LeaseAsync(CancellationToken);

        // Assert
        _channelMock.Verify(c => c.DisposeAsync(), Times.Once);
        next.ShouldBeSameAs(replacement);
        _connectionMock.Verify(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task DisposeAsyncDisposesIdleChannels()
    {
        // Arrange
        Use(Options.Create(new RabbitMqTransportOptions { PublishChannelPoolSize = 1 }));
        Target.Return(await Target.LeaseAsync(CancellationToken));

        // Act
        await Target.DisposeAsync();

        // Assert
        _channelMock.Verify(c => c.DisposeAsync(), Times.Once);
    }
}
