using Microsoft.Extensions.Logging;
using Vulthil.Messaging.Abstractions.Consumers;

namespace WebApi.Infrastructure;

internal sealed class TestConsumer(ILogger<TestConsumer> logger) : IConsumer<TestRequest>
{
    private readonly ILogger<TestConsumer> _logger = logger;

    public Task ConsumeAsync(TestRequest message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Received message: {Message}", message);
        return Task.CompletedTask;
    }
}
