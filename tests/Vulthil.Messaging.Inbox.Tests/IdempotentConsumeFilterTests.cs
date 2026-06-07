using Microsoft.Extensions.Options;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Inbox.Tests;

public sealed class IdempotentConsumeFilterTests : BaseUnitTestCase
{
    private readonly Mock<IIdempotencyStore> _store;
    private readonly Mock<IIdempotencyTransaction> _transaction;
    private readonly Mock<IMessageContext<TestMessage>> _context;
    private bool _consumerInvoked;

    public IdempotentConsumeFilterTests()
    {
        _store = GetMock<IIdempotencyStore>();
        _transaction = GetMock<IIdempotencyTransaction>();
        _context = GetMock<IMessageContext<TestMessage>>();

        _store
            .Setup(store => store.BeginAsync(It.IsAny<IMessageContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_transaction.Object);
        _context.SetupGet(context => context.CancellationToken).Returns(CancellationToken);

        Use<IOptions<InboxOptions>>(Options.Create(new InboxOptions()));
    }

    [Fact]
    public async Task NewMessageInvokesConsumerAndCommitsMarker()
    {
        // Arrange
        _context.SetupGet(context => context.MessageId).Returns("message-1");
        _transaction
            .Setup(transaction => transaction.HasProcessedAsync("message-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await CreateFilter().ConsumeAsync(_context.Object, Next);

        // Assert
        _consumerInvoked.ShouldBeTrue();
        _transaction.Verify(transaction => transaction.CommitAsync("message-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AlreadyProcessedMessageSkipsConsumerAndDoesNotCommit()
    {
        // Arrange
        _context.SetupGet(context => context.MessageId).Returns("message-1");
        _transaction
            .Setup(transaction => transaction.HasProcessedAsync("message-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await CreateFilter().ConsumeAsync(_context.Object, Next);

        // Assert
        _consumerInvoked.ShouldBeFalse();
        _transaction.Verify(transaction => transaction.CommitAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MissingKeyWithRejectEnabledThrowsAndDoesNotBeginTransaction()
    {
        // Arrange
        _context.SetupGet(context => context.MessageId).Returns((string?)null);

        // Act & Assert
        await Should.ThrowAsync<MissingIdempotencyKeyException>(() => CreateFilter().ConsumeAsync(_context.Object, Next));
        _consumerInvoked.ShouldBeFalse();
        _store.Verify(store => store.BeginAsync(It.IsAny<IMessageContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MissingKeyWithRejectDisabledInvokesConsumerWithoutStore()
    {
        // Arrange
        Use<IOptions<InboxOptions>>(Options.Create(new InboxOptions { RejectMessagesWithoutKey = false }));
        _context.SetupGet(context => context.MessageId).Returns((string?)null);

        // Act
        await CreateFilter().ConsumeAsync(_context.Object, Next);

        // Assert
        _consumerInvoked.ShouldBeTrue();
        _store.Verify(store => store.BeginAsync(It.IsAny<IMessageContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CustomKeySelectorIsUsedForDeduplication()
    {
        // Arrange
        _context.SetupGet(context => context.MessageId).Returns("message-1");
        GetMock<IInboxKeySelector<TestMessage>>()
            .Setup(selector => selector.GetKey(It.IsAny<IMessageContext<TestMessage>>()))
            .Returns("business-key");
        _transaction
            .Setup(transaction => transaction.HasProcessedAsync("business-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await CreateFilter().ConsumeAsync(_context.Object, Next);

        // Assert
        _transaction.Verify(transaction => transaction.CommitAsync("business-key", It.IsAny<CancellationToken>()), Times.Once);
    }

    private IdempotentConsumeFilter<TestMessage> CreateFilter() =>
        AutoMocker.CreateInstance<IdempotentConsumeFilter<TestMessage>>();

    private Task Next(IMessageContext<TestMessage> context)
    {
        _consumerInvoked = true;
        return Task.CompletedTask;
    }

    public sealed record TestMessage(string Value);
}
