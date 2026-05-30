using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.IntegrationTest.Contracts;

namespace Vulthil.Messaging.IntegrationTest.ConsumerService.Failures;

public sealed partial class FailingRequestConsumer(
    ILogger<FailingRequestConsumer> logger) : IRequestConsumer<FailingRequest, FailingResponse>
{
    public Task<FailingResponse> ConsumeAsync(IMessageContext<FailingRequest> messageContext, CancellationToken cancellationToken = default)
    {
        LogReceived(logger, messageContext.Message.Reason);
        throw new InvalidOperationException(
            $"Intentional request failure: {messageContext.Message.Reason}");
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "FailingRequest received ({Reason}) — about to throw")]
    private static partial void LogReceived(ILogger logger, string reason);
}
