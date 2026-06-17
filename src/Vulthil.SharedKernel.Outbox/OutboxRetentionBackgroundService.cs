using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Background service that periodically deletes processed and dead-lettered outbox rows older than the configured
/// retention period. Enabled via <see cref="OutboxRetentionOptions"/>; the sweep is skipped when the registered
/// <see cref="IOutboxStore"/> does not implement <see cref="IOutboxRetentionStore"/>.
/// </summary>
internal sealed class OutboxRetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<OutboxRetentionOptions> options,
    ILogger<OutboxRetentionBackgroundService> logger) : BackgroundService
{
    private readonly OutboxRetentionOptions _options = options.Value;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.SweepInterval, timeProvider);
        do
        {
            await SweepSafelyAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SweepAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Outbox retention sweep failed.");
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        if (scope.ServiceProvider.GetRequiredService<IOutboxStore>() is not IOutboxRetentionStore store)
        {
            return;
        }

        var cutoff = timeProvider.GetUtcNow() - _options.RetentionPeriod;
        var total = 0;
        int deleted;
        do
        {
            deleted = await store.DeleteProcessedAsync(cutoff, _options.BatchSize, cancellationToken);
            total += deleted;
        }
        while (deleted >= _options.BatchSize && !cancellationToken.IsCancellationRequested);

        if (total > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Outbox retention deleted {DeletedCount} processed or dead-lettered rows older than {Cutoff:o}.", total, cutoff);
        }
    }
}
