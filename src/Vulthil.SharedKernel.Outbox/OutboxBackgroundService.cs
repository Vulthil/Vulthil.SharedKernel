using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vulthil.Extensions.Hosting;

namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Hosts the outbox relay loop. It derives from <see cref="RestartableBackgroundService"/>, so infrastructure (such as a
/// test harness resetting the database) can pause it around operations that must not run concurrently with it.
/// </summary>
internal sealed class OutboxBackgroundService(
    ILogger<OutboxBackgroundService> logger,
    IServiceScopeFactory serviceScopeFactory,
    IOutboxSignal signal,
    IEnumerable<IOutboxRelayGate> relayGates,
    IOptions<OutboxProcessingOptions> options,
    IHostApplicationLifetime applicationLifetime) : RestartableBackgroundService(applicationLifetime, logger)
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!await TryWaitForRelayGatesAsync(stoppingToken).ConfigureAwait(false))
        {
            return;
        }

        var delay = TimeSpan.Zero;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (delay > TimeSpan.Zero)
                {
                    // Wake early when a transaction commits (low latency); the timeout keeps the poll as a backstop.
                    await signal.WaitAsync(delay, stoppingToken).ConfigureAwait(false);
                }

                var scope = serviceScopeFactory.CreateAsyncScope();
                await using var _ = scope.ConfigureAwait(false);
                var relayCycle = scope.ServiceProvider.GetRequiredService<OutboxRelayCycle>();

                var cycle = await relayCycle.RunAsync(stoppingToken).ConfigureAwait(false);
                delay = OutboxRelayBackoff.After(cycle, delay, options.Value);
            }
            catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
            {
                Logger.LogInformation(ex, "Outbox processing stopped");
                break;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error processing outbox messages");
                delay = OutboxRelayBackoff.AfterFault(options.Value);
            }
        }
    }

    /// <summary>
    /// Waits for every relay gate to open. Returns <see langword="false"/> if the service is stopped while waiting, so
    /// the relay ends without running a cycle.
    /// </summary>
    private async Task<bool> TryWaitForRelayGatesAsync(CancellationToken stoppingToken)
    {
        foreach (var gate in relayGates)
        {
            try
            {
                await gate.WaitUntilReadyAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Outbox relay readiness gate {Gate} failed; starting the relay anyway", gate.GetType().Name);
            }
        }

        return true;
    }
}
