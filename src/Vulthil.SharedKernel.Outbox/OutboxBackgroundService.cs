using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vulthil.Extensions.Hosting;

namespace Vulthil.SharedKernel.Outbox;

internal sealed class OutboxBackgroundService(
    ILogger<OutboxBackgroundService> logger,
    IServiceScopeFactory serviceScopeFactory,
    IOutboxSignal signal,
    IEnumerable<IOutboxRelayGate> relayGates,
    IOptions<OutboxProcessingOptions> options) : BackgroundService, IRestartableHostedService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await TryWaitForRelayGatesAsync(stoppingToken))
        {
            return;
        }

        int baseDelayMs = options.Value.OutboxProcessingDelayInSeconds * 1000;
        int maxDelayMs = options.Value.MaxDelaySeconds * 1000;
        int currentDelayMs = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (currentDelayMs > 0)
                {
                    // Wake early when a transaction commits (low latency); the timeout keeps the poll as a backstop.
                    await signal.WaitAsync(TimeSpan.FromMilliseconds(currentDelayMs), stoppingToken);
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

    /// <summary>
    /// Waits for every relay gate to open. Returns <see langword="false"/> if the service is stopped while waiting, so
    /// the caller exits cleanly instead of letting the cancellation fault the background service and stop the host.
    /// </summary>
    private async Task<bool> TryWaitForRelayGatesAsync(CancellationToken stoppingToken)
    {
        foreach (var gate in relayGates)
        {
            try
            {
                await gate.WaitUntilReadyAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Outbox relay readiness gate {Gate} failed; starting the relay anyway", gate.GetType().Name);
            }
        }

        return true;
    }
}
