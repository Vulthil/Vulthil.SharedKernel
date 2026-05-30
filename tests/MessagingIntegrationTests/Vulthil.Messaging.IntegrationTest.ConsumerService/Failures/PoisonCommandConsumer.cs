using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.IntegrationTest.ConsumerService.Infrastructure;
using Vulthil.Messaging.IntegrationTest.Contracts;

namespace Vulthil.Messaging.IntegrationTest.ConsumerService.Failures;

public sealed partial class PoisonCommandConsumer(
    ILogger<PoisonCommandConsumer> logger,
    ReceivedMessageTracker tracker) : IConsumer<PoisonCommand>
{
    public Task ConsumeAsync(IMessageContext<PoisonCommand> messageContext, CancellationToken cancellationToken = default)
    {
        var attempt = tracker.RecordAttempt(messageContext.Message.Id);
        LogAttempt(logger, messageContext.Message.Id, attempt);

        throw new InvalidOperationException(
            $"Poison message {messageContext.Message.Id} always fails (attempt {attempt}).");
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "PoisonCommand {Id} attempt {Attempt} — about to fail")]
    private static partial void LogAttempt(ILogger logger, Guid id, int attempt);
}
