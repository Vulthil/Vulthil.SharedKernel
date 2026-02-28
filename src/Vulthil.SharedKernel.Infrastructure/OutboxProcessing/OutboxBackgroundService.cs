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
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        int baseDelayMs = options.Value.OutboxProcessingDelayInSeconds * 1000;
        int maxDelayMs = options.Value.MaxDelaySeconds * 1000;
        int currentDelayMs = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (currentDelayMs > 0)
                {
                    await Task.Delay(currentDelayMs, stoppingToken);
                }

                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var outboxProcessor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();

                var processedCount = await outboxProcessor.ExecuteAsync(stoppingToken);

                currentDelayMs = processedCount switch
                {
                    _ when processedCount >= options.Value.BatchSize => 0,
                    0 => Math.Min(Math.Max(currentDelayMs * 2, baseDelayMs), maxDelayMs),
                    _ => baseDelayMs
                };
            }
            catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation(ex, "Outbox processing stopped");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing outbox messages");
                currentDelayMs = baseDelayMs;
            }
        }
    }
}
