using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vulthil.Extensions.Hosting;

namespace Vulthil.Messaging;

/// <summary>
/// Hosts the message <see cref="ITransport"/> for the lifetime of the application: each generation starts the
/// transport (and its consumers) and stops it again when the service stops. It derives from
/// <see cref="RestartableBackgroundService"/>, so infrastructure (such as a test harness resetting the database) can
/// pause message consumption and resume it.
/// </summary>
/// <remarks>
/// <para>
/// Transport startup is retried with capped exponential backoff until it succeeds or the service stops, because a
/// broker that is still coming up is a transient infrastructure condition rather than a reason to fault the host.
/// </para>
/// <para>
/// A generation stops the transport it started before it ends, and the next start waits for the previous generation,
/// so a restart never starts the transport while the previous stop is still running.
/// </para>
/// <para>
/// A hosted service constructor that throws fails DI resolution directly, so <c>IHost.StartAsync</c> throws it as-is,
/// while a failure inside a generation never propagates out of <c>IHost.StartAsync</c>. The clear "no transport
/// registered" error therefore fires from the constructor; it does so via <see cref="IServiceProviderIsService"/>,
/// which answers whether <see cref="ITransport"/> is registered without constructing one. The transport instance
/// itself is resolved lazily, inside the retry loop, so a transport whose construction depends on an unreachable
/// resource (such as a broker connection) is treated as a retryable startup failure instead.
/// </para>
/// </remarks>
internal sealed class ConsumerHostedService : RestartableBackgroundService
{
    private const string NoTransportRegisteredMessage =
        "No messaging transport is registered. Call a transport extension such as .UseRabbitMq(...) " +
        "inside AddMessaging(...) (or .UseTestHarness() in a test).";

    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;

    public ConsumerHostedService(
        IServiceProvider serviceProvider,
        IServiceProviderIsService serviceProviderIsService,
        TimeProvider timeProvider,
        IHostApplicationLifetime applicationLifetime,
        ILogger<ConsumerHostedService> logger)
        : base(applicationLifetime, logger)
    {
        if (!serviceProviderIsService.IsService(typeof(ITransport)))
        {
            throw new InvalidOperationException(NoTransportRegisteredMessage);
        }

        _serviceProvider = serviceProvider;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (await StartTransportAsync(stoppingToken).ConfigureAwait(false) is not { } transport)
        {
            return;
        }

        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await StopTransportAsync(transport).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts the transport, retrying a failed start until it succeeds. Returns the started transport, or
    /// <see langword="null"/> when the service is stopped before a start succeeds.
    /// </summary>
    private async Task<ITransport?> StartTransportAsync(CancellationToken stoppingToken)
    {
        var retryDelay = InitialRetryDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var transport = ResolveTransport();
                await transport.StartAsync(stoppingToken).ConfigureAwait(false);
                return transport;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception exception)
            {
                ConsumerLog.TransportStartFailed(Logger, retryDelay.TotalSeconds, exception);
            }

            if (!await TryDelayAsync(retryDelay, stoppingToken).ConfigureAwait(false))
            {
                return null;
            }

            retryDelay = NextRetryDelay(retryDelay);
        }

        return null;
    }

    /// <summary>
    /// Stops the transport this generation started. A failed stop is logged instead of faulting the service, so a
    /// shutdown or a pause still completes.
    /// </summary>
    private async Task StopTransportAsync(ITransport transport)
    {
        try
        {
            await transport.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            ConsumerLog.TransportStopFailed(Logger, exception);
        }
    }

    /// <summary>
    /// Resolves the transport to start, picking the <em>last</em> registered <see cref="ITransport"/> when more than
    /// one is present. This mirrors the built-in container's own behavior for a singular (non-enumerable)
    /// injection, so it is also what any other single-<see cref="ITransport"/> constructor dependency would
    /// receive. <c>Vulthil.Messaging.TestHarness</c>'s <c>UseTestHarness()</c>/<c>ReplaceTransportWithTestHarness()</c>
    /// depend on this: they remove any transport already registered and add their in-memory one, so calling them
    /// after a broker transport (e.g. <c>UseRabbitMq</c>) leaves the in-memory transport both the only and the last
    /// registration. Registering a transport again afterward (or registering two transports without an intervening
    /// removal) makes the most recently added one win here, silently shadowing the other.
    /// </summary>
    private ITransport ResolveTransport() =>
        _serviceProvider.GetServices<ITransport>().LastOrDefault()
            ?? throw new InvalidOperationException(NoTransportRegisteredMessage);

    private async Task<bool> TryDelayAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(delay, _timeProvider, stoppingToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private static TimeSpan NextRetryDelay(TimeSpan current) =>
        TimeSpan.FromTicks(Math.Min(current.Ticks * 2, MaxRetryDelay.Ticks));
}

internal static partial class ConsumerLog
{
    [LoggerMessage(EventId = 2100, Level = LogLevel.Warning,
        Message = "Message transport startup failed; retrying in {RetryDelaySeconds}s.")]
    public static partial void TransportStartFailed(ILogger logger, double retryDelaySeconds, Exception exception);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Error,
        Message = "Message transport stop failed; message consumption may not have stopped.")]
    public static partial void TransportStopFailed(ILogger logger, Exception exception);
}
