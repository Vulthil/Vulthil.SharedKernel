using Microsoft.Extensions.Hosting;

namespace Vulthil.Messaging;

internal sealed class ConsumerHostedService : BackgroundService
{
    private readonly ITransport _transport;

    public ConsumerHostedService(ITransport transport)
    {
        _transport = transport;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => _transport.StartAsync(stoppingToken);
}
