using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Vulthil.SharedKernel.Infrastructure.OutboxProcessing;
internal sealed class OutboxBackgroundService(
    ILogger<OutboxBackgroundService> logger,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<OutboxProcessingOptions> options) : BackgroundService
{
    private readonly ILogger<OutboxBackgroundService> _logger = logger;
    private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
    private readonly IOptions<OutboxProcessingOptions> _options = options;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await using var scope = _serviceScopeFactory.CreateAsyncScope();
                var outboxProcessor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();

                await outboxProcessor.ExecuteAsync(stoppingToken);

                await Task.Delay(TimeSpan.FromSeconds(_options.Value.OutboxProcessingDelayInSeconds), stoppingToken);
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation(ex, "Outbox processing stopped");
        }
    }
}
