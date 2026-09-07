using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Vulthil.Extensions.Retention;

/// <summary>
/// Hosted service that runs the retention sweep for one store: on start and then once per
/// <see cref="RetentionSweepSettings.SweepInterval"/>, it resolves <typeparamref name="TStore"/> from a fresh scope,
/// asks the adapter for the store's deleter, and deletes entries older than the retention cutoff in bounded batches
/// until fewer than a full batch remain.
/// </summary>
/// <typeparam name="TStore">The registered store service the sweep resolves and deletes through.</typeparam>
internal sealed class RetentionSweepService<TStore>(
    string name,
    RetentionSweepSettings settings,
    Func<TStore, RetentionSweepDeleter?> deleter,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<RetentionSweepService<TStore>> logger) : BackgroundService
    where TStore : notnull
{
    private bool _loggedUnsupportedStore;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(settings.SweepInterval, timeProvider);
            do
            {
                await SweepSafelyAsync(stoppingToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException exception) when (stoppingToken.IsCancellationRequested)
        {
            RetentionSweepLog.LoopStopped(logger, exception, name);
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
            RetentionSweepLog.SweepFailed(logger, exception, name);
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        var scope = scopeFactory.CreateAsyncScope();
        await using var _ = scope.ConfigureAwait(false);
        var delete = deleter(scope.ServiceProvider.GetRequiredService<TStore>());
        if (delete is null)
        {
            if (!_loggedUnsupportedStore)
            {
                _loggedUnsupportedStore = true;
                RetentionSweepLog.StoreUnsupported(logger, name);
            }

            return;
        }

        var cutoff = timeProvider.GetUtcNow() - settings.RetentionPeriod;
        var batchSize = Math.Max(1, settings.BatchSize);
        var total = 0;
        int deleted;
        do
        {
            deleted = await delete(cutoff, batchSize, cancellationToken).ConfigureAwait(false);
            total += deleted;
        }
        while (deleted >= batchSize && !cancellationToken.IsCancellationRequested);

        if (total > 0)
        {
            RetentionSweepLog.Deleted(logger, name, total, cutoff);
        }
    }
}

internal static partial class RetentionSweepLog
{
    [LoggerMessage(EventId = 3001, Level = LogLevel.Debug, Message = "{Name} retention sweep loop stopped.")]
    public static partial void LoopStopped(ILogger logger, Exception exception, string name);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Error, Message = "{Name} retention sweep failed.")]
    public static partial void SweepFailed(ILogger logger, Exception exception, string name);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Warning,
        Message = "{Name} retention is enabled, but the registered store does not support retention; the sweep will not run.")]
    public static partial void StoreUnsupported(ILogger logger, string name);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Information,
        Message = "{Name} retention deleted {DeletedCount} entries older than {Cutoff:o}.")]
    public static partial void Deleted(ILogger logger, string name, int deletedCount, DateTimeOffset cutoff);
}
