using Microsoft.Extensions.Options;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Inbox.Tests;

public sealed class IdempotentConsumeFilterTests : BaseUnitTestCase
{
    private readonly Mock<IIdempotencyStore> _store;
    private readonly Mock<IMessageContext<TestMessage>> _context;
    private bool _consumerInvoked;

    public IdempotentConsumeFilterTests()
    {
        _store = GetMock<IIdempotencyStore>();
        _context = GetMock<IMessageContext<TestMessage>>();
        _context.SetupGet(context => context.CancellationToken).Returns(CancellationToken);

        Use<IOptions<InboxOptions>>(Options.Create(new InboxOptions()));
    }

    [Fact]
    public async Task NewMessageRunsConsumerThroughStore()
    {
        // Arrange
        _context.SetupGet(context => context.MessageId).Returns("message-1");
        SetupStore("message-1", runConsumer: true, processed: true);

        // Act
        await CreateFilter().ConsumeAsync(_context.Object, Next);

        // Assert
        _consumerInvoked.ShouldBeTrue();
        _store.Verify(store => store.ProcessAsync("message-1", It.IsAny<IMessageContext>(), It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AlreadyProcessedMessageSkipsConsumer()
    {
        // Arrange
        _context.SetupGet(context => context.MessageId).Returns("message-1");
        SetupStore("message-1", runConsumer: false, processed: false);

        // Act
        await CreateFilter().ConsumeAsync(_context.Object, Next);

        // Assert
        _consumerInvoked.ShouldBeFalse();
    }

    [Fact]
    public async Task MissingKeyWithRejectEnabledThrowsAndDoesNotCallStore()
    {
        // Arrange
        _context.SetupGet(context => context.MessageId).Returns((string?)null);

        // Act & Assert
        await Should.ThrowAsync<MissingIdempotencyKeyException>(() => CreateFilter().ConsumeAsync(_context.Object, Next));
        _consumerInvoked.ShouldBeFalse();
        _store.Verify(store => store.ProcessAsync(It.IsAny<string>(), It.IsAny<IMessageContext>(), It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
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
        _store.Verify(store => store.ProcessAsync(It.IsAny<string>(), It.IsAny<IMessageContext>(), It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CustomKeySelectorIsUsedForDeduplication()
    {
        // Arrange
        _context.SetupGet(context => context.MessageId).Returns("message-1");
        GetMock<IInboxKeySelector<TestMessage>>()
            .Setup(selector => selector.GetKey(It.IsAny<IMessageContext<TestMessage>>()))
            .Returns("business-key");
        SetupStore("business-key", runConsumer: true, processed: true);

        // Act
        await CreateFilter().ConsumeAsync(_context.Object, Next);

        // Assert
        _store.Verify(store => store.ProcessAsync("business-key", It.IsAny<IMessageContext>(), It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetupStore(string key, bool runConsumer, bool processed) =>
        _store
            .Setup(store => store.ProcessAsync(key, It.IsAny<IMessageContext>(), It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, IMessageContext _, Func<CancellationToken, Task> process, CancellationToken token) =>
            {
                if (runConsumer)
                {
                    await process(token);
                }

                return processed;
            });

    private IdempotentConsumeFilter<TestMessage> CreateFilter() =>
        AutoMocker.CreateInstance<IdempotentConsumeFilter<TestMessage>>();

    private Task Next(IMessageContext<TestMessage> context)
    {
        _consumerInvoked = true;
        return Task.CompletedTask;
    }

    public sealed record TestMessage(string Value);
}
