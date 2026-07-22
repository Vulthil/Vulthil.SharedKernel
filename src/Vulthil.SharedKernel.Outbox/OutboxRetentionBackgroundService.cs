using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Background service that periodically deletes processed and dead-lettered outbox rows older than the configured
/// retention period. Registered by <c>AddOutboxEngine</c> only when <see cref="OutboxProcessingOptions.Retention"/>
/// is enabled; the sweep is skipped when the registered <see cref="IOutboxStore"/> does not implement
/// <see cref="IOutboxRetentionStore"/>.
/// </summary>
internal sealed class OutboxRetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<OutboxProcessingOptions> options,
    ILogger<OutboxRetentionBackgroundService> logger) : BackgroundService
{
    private readonly OutboxRetentionOptions _options = options.Value.Retention;
    private bool _loggedMissingRetentionStore;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(_options.SweepInterval, timeProvider);
            do
            {
                await SweepSafelyAsync(stoppingToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException exception) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogDebug(exception, "Outbox retention sweep loop stopped");
        }
    }

    /// <summary>
    /// Runs one sweep, treating every failure as transient (logged, retried on the next tick). Only a cancellation
    /// caused by the service stopping propagates; a foreign <see cref="OperationCanceledException"/> — for example a
    /// store client surfacing a timeout as a cancellation — must not escape, because the host would treat the
    /// canceled execute task of a still-running application as a fault and stop the host.
    /// </summary>
    private async Task SweepSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SweepAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Outbox retention sweep failed.");
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        var scope = scopeFactory.CreateAsyncScope();
        await using var _ = scope.ConfigureAwait(false);
        if (scope.ServiceProvider.GetRequiredService<IOutboxStore>() is not IOutboxRetentionStore store)
        {
            if (!_loggedMissingRetentionStore)
            {
                _loggedMissingRetentionStore = true;
                logger.LogWarning("Outbox retention is enabled, but the registered IOutboxStore does not implement IOutboxRetentionStore; the retention sweep will not run.");
            }

            return;
        }

        var cutoff = timeProvider.GetUtcNow() - _options.RetentionPeriod;
        var batchSize = Math.Max(1, _options.BatchSize);
        var total = 0;
        int deleted;
        do
        {
            deleted = await store.DeleteProcessedAsync(cutoff, batchSize, cancellationToken).ConfigureAwait(false);
            total += deleted;
        }
        while (deleted >= batchSize && !cancellationToken.IsCancellationRequested);

        if (total > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Outbox retention deleted {DeletedCount} processed or dead-lettered rows older than {Cutoff:o}.", total, cutoff);
        }
    }
}
