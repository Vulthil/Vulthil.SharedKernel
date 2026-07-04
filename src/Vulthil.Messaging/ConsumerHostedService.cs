using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Vulthil.Messaging;

/// <summary>
/// Hosts the message <see cref="ITransport"/> for the lifetime of the application, starting the transport (and its
/// consumers) on startup.
/// </summary>
/// <remarks>
/// Transport startup is retried with capped exponential backoff until it succeeds or the host stops, because a broker
/// that is still coming up is a transient infrastructure condition rather than a reason to fault the host. The host's
/// <c>BackgroundServiceExceptionBehavior</c> is left untouched, so a genuine fault raised once the transport is running
/// still surfaces and stops the host as usual.
/// <para>
/// The generic host swallows exceptions thrown from inside <see cref="BackgroundService.ExecuteAsync"/> — even ones
/// thrown synchronously before the first await — logging them and stopping the host without ever propagating them out
/// of <c>IHost.StartAsync</c>. A hosted service constructor throwing is not subject to that: it fails DI resolution
/// directly, so <c>IHost.StartAsync</c> throws it as-is. The clear "no transport registered" error therefore still
/// needs to fire from the constructor to surface synchronously; it does so via <see cref="IServiceProviderIsService"/>,
/// which answers whether <see cref="ITransport"/> is registered without constructing one. The transport instance
/// itself is resolved lazily, inside the retry loop, so a transport whose construction depends on an unreachable
/// resource (such as a broker connection) is treated as a retryable startup failure instead.
/// </para>
/// </remarks>
internal sealed class ConsumerHostedService : BackgroundService
{
    private const string NoTransportRegisteredMessage =
        "No messaging transport is registered. Call a transport extension such as .UseRabbitMq(...) " +
        "inside AddMessaging(...) (or .UseTestHarness() in a test).";

    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ConsumerHostedService> _logger;

    public ConsumerHostedService(
        IServiceProvider serviceProvider,
        IServiceProviderIsService serviceProviderIsService,
        TimeProvider timeProvider,
        ILogger<ConsumerHostedService> logger)
    {
        if (!serviceProviderIsService.IsService(typeof(ITransport)))
        {
            throw new InvalidOperationException(NoTransportRegisteredMessage);
        }

        _serviceProvider = serviceProvider;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retryDelay = InitialRetryDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ResolveTransport().StartAsync(stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                ConsumerLog.TransportStartFailed(_logger, retryDelay.TotalSeconds, exception);
            }

            if (!await TryDelayAsync(retryDelay, stoppingToken))
            {
                return;
            }

            retryDelay = NextRetryDelay(retryDelay);
        }
    }

    private ITransport ResolveTransport() =>
        _serviceProvider.GetServices<ITransport>().LastOrDefault()
            ?? throw new InvalidOperationException(NoTransportRegisteredMessage);

    private async Task<bool> TryDelayAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(delay, _timeProvider, stoppingToken);
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
}
