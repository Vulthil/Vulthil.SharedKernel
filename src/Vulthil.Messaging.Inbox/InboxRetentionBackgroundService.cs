using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Vulthil.Messaging.Inbox;

/// <summary>
/// Background service that periodically deletes idempotency markers older than the configured retention period.
/// Registered by <c>AddRelationalInbox</c>/<c>AddCosmosInbox</c> only when <see cref="InboxOptions.Retention"/> is
/// enabled; the sweep is skipped when the registered <see cref="IIdempotencyStore"/> does not implement
/// <see cref="IInboxRetentionStore"/>.
/// </summary>
internal sealed class InboxRetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    IOptions<InboxOptions> options,
    ILogger<InboxRetentionBackgroundService> logger) : BackgroundService
{
    private readonly InboxRetentionOptions _options = options.Value.Retention;

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
        var batchSize = Math.Max(1, _options.BatchSize);
        var total = 0;
        int deleted;
        do
        {
            deleted = await store.DeleteProcessedAsync(cutoff, batchSize, cancellationToken);
            total += deleted;
        }
        while (deleted >= batchSize && !cancellationToken.IsCancellationRequested);

        if (total > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Inbox retention deleted {DeletedCount} markers older than {Cutoff:o}.", total, cutoff);
        }
    }
}
