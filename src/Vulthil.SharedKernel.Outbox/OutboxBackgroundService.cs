using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vulthil.Extensions.Hosting;

namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Hosts the outbox relay loop, restartable via <see cref="IRestartableHostedService"/> so infrastructure (such as a
/// test harness resetting the database) can pause it around operations that must not run concurrently with it.
/// </summary>
/// <remarks>
/// <para>
/// The service implements <see cref="IHostedService"/> directly instead of inheriting
/// <see cref="BackgroundService"/> because <see cref="BackgroundService"/> is not restart-safe: the host observes
/// only the execute task created by the first <c>StartAsync</c>, and on .NET 10 that task is scheduled with
/// <c>Task.Run(..., stoppingToken)</c> — a stop that lands before the thread pool has run the delegate transitions
/// the observed task straight to canceled without <c>ExecuteAsync</c> ever running, which the host (whose default
/// <c>BackgroundServiceExceptionBehavior</c> is <c>StopHost</c>) treats as a fault and stops the application while
/// it is still serving. Owning the lifecycle removes both hazards: the execute task always runs the relay loop
/// (cancellation is observed inside it and always ends in a graceful return), and the host never awaits the task.
/// A genuine fault escaping the loop still stops the application — matching the <see cref="BackgroundService"/>
/// default — via <see cref="IHostApplicationLifetime.StopApplication"/>.
/// </para>
/// <para>
/// The lifecycle methods are idempotent and safe to overlap, because the harness restart cycle and the host's final
/// stop are not serialized with each other by any contract: a lock guards every transition, the stopping source and
/// execute task are only ever swapped together (so a stop always cancels the same loop generation it awaits), a
/// restart retires the previous generation by canceling it, and disposal marks the service so a later stop only
/// awaits the already-canceled loop and a later start is a no-op. Without this, a stop that overlapped a restart or
/// ran after disposal could await <see cref="CancellationTokenSource.CancelAsync"/> on a disposed source and fail
/// the host's <c>StopAsync</c> with an <see cref="ObjectDisposedException"/> at teardown, or pair the cancellation
/// of one generation with the wait for another and stall until its caller's token fired.
/// </para>
/// <para>
/// Retired stopping sources are canceled but never disposed: disposing a source whose cancellation notification is
/// still queued silently drops the pending callbacks (callback execution atomically claims the registration store,
/// and <c>Dispose</c> clears that same store), which would strand the retired loop in a wait that no longer ends.
/// They hold no timer or kernel handle, so unreferenced retired sources are reclaimed by garbage collection. Only
/// <see cref="Dispose"/> disposes a source, and only one whose cancellation it also requested itself — the
/// synchronous first-caller <see cref="CancellationTokenSource.Cancel()"/> runs the callbacks to completion before
/// the disposal. A source some stop already canceled may still have its notification in flight, so it is left to
/// garbage collection like the retired ones.
/// </para>
/// </remarks>
internal sealed class OutboxBackgroundService(
    ILogger<OutboxBackgroundService> logger,
    IServiceScopeFactory serviceScopeFactory,
    IOutboxSignal signal,
    IEnumerable<IOutboxRelayGate> relayGates,
    IOptions<OutboxProcessingOptions> options,
    IHostApplicationLifetime applicationLifetime) : IRestartableHostedService, IDisposable
{
    private readonly Lock _lifecycleGate = new();

    private CancellationTokenSource? _stoppingCts;
    private bool _disposed;

    /// <summary>
    /// Gets the task running the current relay loop, or <see langword="null"/> before the first start. The task
    /// always completes successfully: cancellation ends it with a graceful return and a genuine fault is handled
    /// inside it, so no caller ever observes an exception from a stopped relay.
    /// </summary>
    internal Task? ExecuteTask { get; private set; }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? previous;
        lock (_lifecycleGate)
        {
            if (_disposed)
            {
                return;
            }

            previous = _stoppingCts;
            _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var stoppingToken = _stoppingCts.Token;
            ExecuteTask = Task.Run(() => ExecuteAsync(stoppingToken), CancellationToken.None);
        }

        if (previous is not null)
        {
            await previous.CancelAsync();
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Task? executeTask;
        var cancellation = Task.CompletedTask;
        lock (_lifecycleGate)
        {
            executeTask = ExecuteTask;
            if (executeTask is not null && !_disposed && _stoppingCts is { } stoppingCts)
            {
                cancellation = stoppingCts.CancelAsync();
            }
        }

        if (executeTask is null)
        {
            return;
        }

        try
        {
            await cancellation;
        }
        finally
        {
            await executeTask.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lifecycleGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_stoppingCts is { IsCancellationRequested: false })
            {
                _stoppingCts.Cancel();
                _stoppingCts.Dispose();
            }
        }
    }

    /// <summary>
    /// Runs the relay loop and guarantees the execute task completes successfully: a cancellation escaping the loop
    /// after the service is stopped is swallowed, while any other exception is a genuine fault that stops the
    /// application, mirroring the host's default behavior for faulted background services.
    /// </summary>
    private async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ProcessUntilStoppedAsync(stoppingToken);
        }
        catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Outbox relay stopped before the processing loop observed the cancellation");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Outbox relay faulted; stopping the application");
            applicationLifetime.StopApplication();
        }
    }

    private async Task ProcessUntilStoppedAsync(CancellationToken stoppingToken)
    {
        if (!await TryWaitForRelayGatesAsync(stoppingToken))
        {
            return;
        }

        int baseDelayMs = options.Value.OutboxProcessingDelaySeconds * 1000;
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
