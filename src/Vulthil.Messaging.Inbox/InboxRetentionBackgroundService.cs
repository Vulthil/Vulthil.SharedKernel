using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Vulthil.Messaging.Inbox;

/// <summary>
/// Background service that periodically deletes idempotency markers older than the configured retention period.
/// Enabled via <see cref="InboxRetentionOptions"/>; the sweep is skipped when the registered
/// <see cref="IIdempotencyStore"/> does not implement <see cref="IInboxRetentionStore"/>.
/// </summary>
internal sealed class InboxRetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<InboxRetentionOptions> options,
    ILogger<InboxRetentionBackgroundService> logger) : BackgroundService
{
    private readonly InboxRetentionOptions _options = options.Value;

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
            logger.LogError(exception, "Inbox retention sweep failed.");
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        if (scope.ServiceProvider.GetRequiredService<IIdempotencyStore>() is not IInboxRetentionStore store)
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
            logger.LogInformation("Inbox retention deleted {DeletedCount} markers older than {Cutoff:o}.", total, cutoff);
        }
    }
}
