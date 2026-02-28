using Microsoft.Extensions.Hosting;

namespace Vulthil.Messaging;

internal sealed class ConsumerHostedService : BackgroundService
{
    private readonly ITransport _transport;

    /// <summary>
    /// Initializes a new instance with the specified transport.
    /// </summary>
    /// <param name="transport">The transport responsible for consuming messages.</param>
    public ConsumerHostedService(ITransport transport)
    {
        _transport = transport;
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => _transport.StartAsync(stoppingToken);
}
