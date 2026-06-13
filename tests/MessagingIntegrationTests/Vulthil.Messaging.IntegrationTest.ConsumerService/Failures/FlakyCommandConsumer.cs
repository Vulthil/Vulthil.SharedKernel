using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.IntegrationTest.ConsumerService.Infrastructure;
using Vulthil.Messaging.IntegrationTest.Contracts;

namespace Vulthil.Messaging.IntegrationTest.ConsumerService.Failures;

public sealed partial class FlakyCommandConsumer(
    ILogger<FlakyCommandConsumer> logger,
    ReceivedMessageTracker tracker) : IConsumer<FlakyCommand>
{
    public Task ConsumeAsync(IMessageContext<FlakyCommand> messageContext, CancellationToken cancellationToken = default)
    {
        var attempt = tracker.RecordAttempt(messageContext.Message.Id);
        LogAttempt(logger, messageContext.Message.Id, attempt, messageContext.RetryCount);

        if (attempt < messageContext.Message.FailUntilAttempt)
        {
            throw new InvalidOperationException(
                $"Flaky failure on attempt {attempt} for {messageContext.Message.Id}.");
        }

        tracker.Record("flaky", messageContext.Message);
        return Task.CompletedTask;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "FlakyCommand {Id} attempt {Attempt} (retryCount {RetryCount})")]
    private static partial void LogAttempt(ILogger logger, Guid id, int attempt, int retryCount);
}
