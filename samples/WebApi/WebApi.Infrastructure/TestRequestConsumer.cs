using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Consumers;

namespace WebApi.Infrastructure;

internal sealed class TestRequestConsumer(ILogger<TestRequestConsumer> logger) : IRequestConsumer<TestRequest, TestEvent>
{
    private readonly ILogger<TestRequestConsumer> _logger = logger;
    public Task<TestEvent> ConsumeAsync(TestRequest message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Received request: {Message}", message);
        return Task.FromResult(new TestEvent(Guid.NewGuid(), message.Name));
    }
}
