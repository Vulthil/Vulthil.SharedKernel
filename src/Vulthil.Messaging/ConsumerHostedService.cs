using Microsoft.Extensions.Hosting;

namespace Vulthil.Messaging;

internal sealed class ConsumerHostedService : BackgroundService
{
    private readonly ITransport _transport;

    public ConsumerHostedService(IEnumerable<ITransport> transports)
    {
        _transport = transports.LastOrDefault()
            ?? throw new InvalidOperationException(
                "No messaging transport is registered. Call a transport extension such as .UseRabbitMq(...) " +
                "inside AddMessaging(...) (or .UseTestHarness() in a test).");
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => _transport.StartAsync(stoppingToken);
}
