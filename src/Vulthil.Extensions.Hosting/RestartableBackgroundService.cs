using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Vulthil.Extensions.Hosting;

/// <summary>
/// A restart-safe base class for a long-running <see cref="IRestartableHostedService"/>, used in place of
/// <see cref="BackgroundService"/>. Every start runs <see cref="ExecuteAsync"/> as a new generation; a stop cancels the
/// running generation and waits for it, and a start first waits for the previous generation to finish, so two
/// generations never run at the same time.
/// </summary>
/// <remarks>
/// <para>
/// The execute task always completes successfully: a cancellation that escapes <see cref="ExecuteAsync"/> after a stop
/// ends it gracefully, and any other exception is a genuine fault that is logged and stops the application through
/// <see cref="IHostApplicationLifetime.StopApplication"/>, matching the host's default for a faulted
/// <see cref="BackgroundService"/>. The host never awaits the execute task, so a stop that lands before the thread pool
/// has run a generation cannot fault the host the way it faults a <see cref="BackgroundService"/> (see
/// <see cref="IRestartableHostedService"/>).
/// </para>
/// <para>
/// The lifecycle methods are idempotent and safe to overlap, because a pause by infrastructure (such as a test harness
/// resetting the database) and the host's final stop are not serialized with each other by any contract: a lock guards
/// every transition, the stopping source and execute task are only ever swapped together (so a stop always cancels the
/// same generation it awaits), a restart retires the previous generation by canceling it, of two overlapping starts
/// only the first to finish waiting begins a generation, and disposal marks the service so a later stop only awaits the
/// already-canceled generation and a later start is a no-op. Without this, a stop that
/// overlapped a restart or ran after disposal could await <see cref="CancellationTokenSource.CancelAsync"/> on a
/// disposed source and fail the host's <c>StopAsync</c> with an <see cref="ObjectDisposedException"/> at teardown, or
/// pair the cancellation of one generation with the wait for another and stall until its caller's token fired.
/// </para>
/// <para>
/// Retired stopping sources are canceled but never disposed: disposing a source whose cancellation notification is
/// still queued silently drops the pending callbacks (callback execution atomically claims the registration store,
/// and <c>Dispose</c> clears that same store), which would strand the retired generation in a wait that no longer
/// ends. They hold no timer or kernel handle, so unreferenced retired sources are reclaimed by garbage collection. Only
/// <see cref="Dispose(bool)"/> disposes a source, and only one whose cancellation it also requested itself — the
/// synchronous first-caller <see cref="CancellationTokenSource.Cancel()"/> runs the callbacks to completion before the
/// disposal. A source some stop already canceled may still have its notification in flight, so it is left to garbage
/// collection like the retired ones.
/// </para>
/// <para>
/// A caller's <see cref="StopAsync"/> can return while the execute task it was waiting for is still winding down — its
/// own wait is bounded by the caller's token, not by how long the generation actually takes to observe cancellation.
/// So <see cref="StartAsync"/> never assumes a prior generation has finished just because a prior stop returned: it
/// cancels that generation (idempotent if a stop already did) and, if its execute task has not completed, waits for it
/// before starting a new one — otherwise two generations would run concurrently against the same resources for as long
/// as the stale one takes to notice its own cancellation, which is exactly the overlap a pause exists to prevent.
/// </para>
/// </remarks>
public abstract class RestartableBackgroundService : IRestartableHostedService, IDisposable
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly string _serviceName;
    private readonly Lock _lifecycleGate = new();

    private CancellationTokenSource? _stoppingCts;
    private bool _disposed;

    /// <summary>
    /// Initializes the service.
    /// </summary>
    /// <param name="applicationLifetime">The application lifetime, stopped when a generation faults.</param>
    /// <param name="logger">The logger for the service's lifecycle events and for <see cref="Logger"/>.</param>
    protected RestartableBackgroundService(IHostApplicationLifetime applicationLifetime, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(applicationLifetime);
        ArgumentNullException.ThrowIfNull(logger);

        _applicationLifetime = applicationLifetime;
        _serviceName = GetType().Name;
        Logger = logger;
    }

    /// <summary>
    /// Gets the task running the current generation, or <see langword="null"/> before the first start. The task always
    /// completes successfully: cancellation ends it with a graceful return and a genuine fault is handled inside it, so
    /// no caller ever observes an exception from a stopped service.
    /// </summary>
    public Task? ExecuteTask { get; private set; }

    /// <summary>
    /// Gets the logger passed to the constructor.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Runs one generation of the service until <paramref name="stoppingToken"/> is canceled. Each start calls it again
    /// with a new token. A generation releases everything it acquired before it returns, because the next generation
    /// starts only after this one has finished.
    /// </summary>
    /// <param name="stoppingToken">Canceled when the service is stopped, restarted or disposed.</param>
    /// <returns>A task that completes when the generation has finished.</returns>
    protected abstract Task ExecuteAsync(CancellationToken stoppingToken);

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? previous;
        Task? previousExecuteTask;
        lock (_lifecycleGate)
        {
            if (_disposed)
            {
                return;
            }

            previous = _stoppingCts;
            previousExecuteTask = ExecuteTask;
        }

        if (previous is not null)
        {
            await previous.CancelAsync().ConfigureAwait(false);
        }

        if (previousExecuteTask is { IsCompleted: false })
        {
            await previousExecuteTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        lock (_lifecycleGate)
        {
            // A start that overlapped this one has already begun a new generation; beginning another would run two
            // generations at once.
            if (_disposed || !ReferenceEquals(_stoppingCts, previous))
            {
                return;
            }

            _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var stoppingToken = _stoppingCts.Token;
            ExecuteTask = Task.Run(() => RunGenerationAsync(stoppingToken), CancellationToken.None);
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
            await cancellation.ConfigureAwait(false);
        }
        finally
        {
            await executeTask.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Marks the service disposed, so a later start does nothing and a later stop only waits for the canceled
    /// generation, and cancels the running generation.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

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
    /// Runs one generation and guarantees its task completes successfully: a cancellation escaping the generation
    /// after the service is stopped is swallowed, while any other exception is a genuine fault that stops the
    /// application, mirroring the host's default behavior for faulted background services.
    /// </summary>
    private async Task RunGenerationAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ExecuteAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (stoppingToken.IsCancellationRequested)
        {
            RestartableBackgroundServiceLog.StoppedBeforeCancellationObserved(Logger, _serviceName, ex);
        }
        catch (Exception ex)
        {
            RestartableBackgroundServiceLog.Faulted(Logger, _serviceName, ex);
            _applicationLifetime.StopApplication();
        }
    }
}

internal static partial class RestartableBackgroundServiceLog
{
    [LoggerMessage(EventId = 4000, Level = LogLevel.Debug,
        Message = "{Service} stopped before its execution observed the cancellation")]
    public static partial void StoppedBeforeCancellationObserved(ILogger logger, string service, Exception exception);

    [LoggerMessage(EventId = 4001, Level = LogLevel.Critical,
        Message = "{Service} faulted; stopping the application")]
    public static partial void Faulted(ILogger logger, string service, Exception exception);
}
