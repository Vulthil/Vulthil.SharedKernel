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
/// </remarks>
internal sealed class ConsumerHostedService : BackgroundService
{
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    private readonly ITransport _transport;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ConsumerHostedService> _logger;

    public ConsumerHostedService(
        IEnumerable<ITransport> transports,
        TimeProvider timeProvider,
        ILogger<ConsumerHostedService> logger)
    {
        _transport = transports.LastOrDefault()
            ?? throw new InvalidOperationException(
                "No messaging transport is registered. Call a transport extension such as .UseRabbitMq(...) " +
                "inside AddMessaging(...) (or .UseTestHarness() in a test).");
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
                await _transport.StartAsync(stoppingToken);
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
