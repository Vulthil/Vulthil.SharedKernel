using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.Messaging.Filters;
using Vulthil.xUnit;

namespace Vulthil.Messaging.Tests.Filters;

public sealed class LoggingConsumeFilterTests : BaseUnitTestCase
{
    internal sealed record TestMessage(string Content);

    private sealed class StubMessageContext : IMessageContext<TestMessage>
    {
        public TestMessage Message { get; init; } = new("");
        public string? MessageId { get; init; }
        public string? RequestId { get; init; }
        public string? ConversationId { get; init; }
        public string? CorrelationId { get; init; }
        public string? InitiatorId { get; init; }
        public Uri? SourceAddress { get; init; }
        public Uri? DestinationAddress { get; init; }
        public Uri? ResponseAddress { get; init; }
        public Uri? FaultAddress { get; init; }
        public string RoutingKey { get; init; } = string.Empty;
        public IDictionary<string, object?> Headers { get; init; } = new Dictionary<string, object?>();
        public DateTimeOffset? SentTime { get; init; }
        public DateTimeOffset? ExpirationTime { get; init; }
        public int RetryCount { get; init; }
        public bool Redelivered { get; init; }
        public CancellationToken CancellationToken { get; init; }
        public Task PublishAsync<TMsg>(TMsg message, Func<IPublishContext, ValueTask>? configure = null) where TMsg : notnull
            => Task.CompletedTask;
    }

    private sealed record LogRecord(LogLevel Level, EventId EventId, Exception? Exception, string Message);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogRecord> Records { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Records.Add(new LogRecord(logLevel, eventId, exception, formatter(state, exception)));
    }

    private static LoggingConsumeFilter<TestMessage> CreateFilter(ILogger<LoggingConsumeFilter<TestMessage>> logger, bool enabled)
    {
        var options = new MessagingOptions();
        options.ConsumeFilters.EnableLogging = enabled;
        return new LoggingConsumeFilter<TestMessage>(logger, Options.Create(options));
    }

    [Fact]
    public async Task DisabledFilterPassesThroughWithoutLogging()
    {
        // Arrange
        var logger = new RecordingLogger<LoggingConsumeFilter<TestMessage>>();
        var filter = CreateFilter(logger, enabled: false);
        var nextCalled = false;

        // Act
        await filter.ConsumeAsync(new StubMessageContext(), _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Assert
        nextCalled.ShouldBeTrue();
        logger.Records.ShouldBeEmpty();
    }

    [Fact]
    public async Task EnabledFilterLogsConsumingAndConsumedOnSuccess()
    {
        // Arrange
        var logger = new RecordingLogger<LoggingConsumeFilter<TestMessage>>();
        var filter = CreateFilter(logger, enabled: true);

        // Act
        await filter.ConsumeAsync(
            new StubMessageContext { MessageId = "m-1", CorrelationId = "c-1" },
            _ => Task.CompletedTask);

        // Assert
        logger.Records.Count.ShouldBe(2);
        logger.Records[0].Level.ShouldBe(LogLevel.Debug);
        logger.Records[0].Message.ShouldContain("Consuming");
        logger.Records[1].Level.ShouldBe(LogLevel.Debug);
        logger.Records[1].Message.ShouldContain("Consumed");
    }

    [Fact]
    public async Task EnabledFilterLogsWarningAndRethrowsOnException()
    {
        // Arrange
        var logger = new RecordingLogger<LoggingConsumeFilter<TestMessage>>();
        var filter = CreateFilter(logger, enabled: true);
        var boom = new InvalidOperationException("boom");

        // Act
        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            () => filter.ConsumeAsync(new StubMessageContext(), _ => throw boom));

        // Assert
        thrown.ShouldBe(boom);
        logger.Records.Count.ShouldBe(2);
        logger.Records[0].Level.ShouldBe(LogLevel.Debug);  // Consuming
        logger.Records[1].Level.ShouldBe(LogLevel.Warning); // ConsumeFailed
        logger.Records[1].Exception.ShouldBe(boom);
    }
}
