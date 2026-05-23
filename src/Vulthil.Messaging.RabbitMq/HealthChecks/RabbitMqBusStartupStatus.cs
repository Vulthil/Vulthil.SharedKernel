namespace Vulthil.Messaging.RabbitMq.HealthChecks;

/// <summary>
/// Tracks completion of <see cref="RabbitMqBus"/> startup so health probes can observe readiness.
/// </summary>
internal sealed class RabbitMqBusStartupStatus
{
    private readonly TaskCompletionSource _readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// A task that completes successfully when the bus has finished its <c>StartAsync</c> pass,
    /// or faults with the exception that prevented startup.
    /// </summary>
    public Task Ready => _readyTcs.Task;

    public void MarkStarted() => _readyTcs.TrySetResult();

    public void MarkFailed(Exception exception) => _readyTcs.TrySetException(exception);
}
