using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.IntegrationTest.ConsumerService.Infrastructure;
using Vulthil.Messaging.IntegrationTest.Contracts;

namespace Vulthil.Messaging.IntegrationTest.ConsumerService.Events;

public sealed partial class OrderedEventConsumer(
    ILogger<OrderedEventConsumer> logger,
    ReceivedMessageTracker tracker) : IConsumer<OrderedEvent>
{
    public async Task ConsumeAsync(IMessageContext<OrderedEvent> messageContext, CancellationToken cancellationToken = default)
    {
        var message = messageContext.Message;
        LogReceived(logger, message.Key, message.Sequence);

        // Fail the first FailAttempts invocations. With in-memory retry the delivery is held (and the lane
        // with it), so a later same-key message cannot be recorded before this one finally succeeds.
        var attempt = tracker.RecordAttempt($"{message.Key}:{message.Sequence}");
        if (attempt <= message.FailAttempts)
        {
            throw new InvalidOperationException($"Ordered {message.Key}#{message.Sequence} failing attempt {attempt}.");
        }

        // Even sequences process slower than odd ones. Without per-key ordering and with queue
        // concurrency, a faster later message would overtake a slower earlier one and be recorded
        // out of order — so a strictly increasing recorded sequence proves the partitioner works.
        var delayMs = message.Sequence % 2 == 0 ? 100 : 10;
        await Task.Delay(delayMs, cancellationToken);

        tracker.Record($"ordered-{message.Key}", message.Sequence);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Received OrderedEvent {Key}#{Sequence}")]
    private static partial void LogReceived(ILogger logger, string key, int sequence);
}
