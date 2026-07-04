using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Inbox.Tests;

public sealed class IdempotentConsumeFilterTests : BaseUnitTestCase
{
    private readonly Lazy<IdempotentConsumeFilter<TestMessage>> _lazyTarget;
    private readonly Mock<IIdempotencyStore> _store;
    private readonly Mock<IMessageContext<TestMessage>> _context;
    private bool _consumerInvoked;

    private IdempotentConsumeFilter<TestMessage> Target => _lazyTarget.Value;

    public IdempotentConsumeFilterTests()
    {
        _lazyTarget = new(CreateInstance<IdempotentConsumeFilter<TestMessage>>);
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
        await Target.ConsumeAsync(_context.Object, Next);

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
        await Target.ConsumeAsync(_context.Object, Next);

        // Assert
        _consumerInvoked.ShouldBeFalse();
    }

    [Fact]
    public async Task MissingKeyWithRejectEnabledThrowsAndDoesNotCallStore()
    {
        // Arrange
        _context.SetupGet(context => context.MessageId).Returns((string?)null);

        // Act & Assert
        await Should.ThrowAsync<MissingIdempotencyKeyException>(() => Target.ConsumeAsync(_context.Object, Next));
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
        await Target.ConsumeAsync(_context.Object, Next);

        // Assert
        _consumerInvoked.ShouldBeTrue();
        _store.Verify(store => store.ProcessAsync(It.IsAny<string>(), It.IsAny<IMessageContext>(), It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EmptyStringKeySelectorResultFallsBackToMessageId()
    {
        // Arrange
        _context.SetupGet(context => context.MessageId).Returns("message-1");
        GetMock<IInboxKeySelector<TestMessage>>()
            .Setup(selector => selector.GetKey(It.IsAny<IMessageContext<TestMessage>>()))
            .Returns(string.Empty);
        SetupStore("message-1", runConsumer: true, processed: true);

        // Act
        await Target.ConsumeAsync(_context.Object, Next);

        // Assert
        _consumerInvoked.ShouldBeTrue();
        _store.Verify(store => store.ProcessAsync("message-1", It.IsAny<IMessageContext>(), It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
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
        await Target.ConsumeAsync(_context.Object, Next);

        // Assert
        _store.Verify(store => store.ProcessAsync("business-key", It.IsAny<IMessageContext>(), It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NewMessageIncrementsTheProcessedCounter()
    {
        // Arrange
        _context.SetupGet(context => context.MessageId).Returns("message-1");
        SetupStore("message-1", runConsumer: true, processed: true);

        // Act
        var count = await MeasureCounterAsync("vulthil.inbox.processed", () => Target.ConsumeAsync(_context.Object, Next));

        // Assert
        count.ShouldBe(1);
    }

    [Fact]
    public async Task AlreadyProcessedMessageIncrementsTheDuplicateSkippedCounter()
    {
        // Arrange
        _context.SetupGet(context => context.MessageId).Returns("message-1");
        SetupStore("message-1", runConsumer: false, processed: false);

        // Act
        var count = await MeasureCounterAsync("vulthil.inbox.duplicate_skipped", () => Target.ConsumeAsync(_context.Object, Next));

        // Assert
        count.ShouldBe(1);
    }

    [Fact]
    public async Task MissingKeyWithRejectDisabledIncrementsTheMissingKeyCounter()
    {
        // Arrange
        Use<IOptions<InboxOptions>>(Options.Create(new InboxOptions { RejectMessagesWithoutKey = false }));
        _context.SetupGet(context => context.MessageId).Returns((string?)null);

        // Act
        var count = await MeasureCounterAsync("vulthil.inbox.missing_key", () => Target.ConsumeAsync(_context.Object, Next));

        // Assert
        count.ShouldBe(1);
    }

    private static async Task<long> MeasureCounterAsync(string instrumentName, Func<Task> action)
    {
        long total = 0;
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == InboxTelemetry.MeterName && instrument.Name == instrumentName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => total += measurement);
        listener.Start();

        await action();

        return total;
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

    private Task Next(IMessageContext<TestMessage> context)
    {
        _consumerInvoked = true;
        return Task.CompletedTask;
    }

    public sealed record TestMessage(string Value);
}
