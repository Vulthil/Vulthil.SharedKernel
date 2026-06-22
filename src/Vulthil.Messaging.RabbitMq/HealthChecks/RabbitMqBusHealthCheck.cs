using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Vulthil.Messaging.RabbitMq.HealthChecks;

/// <summary>
/// Reports the readiness of the Vulthil RabbitMQ bus, transitioning to <see cref="HealthStatus.Healthy"/>
/// after <see cref="RabbitMqBus"/>.<c>StartAsync</c> completes successfully.
/// </summary>
internal sealed class RabbitMqBusHealthCheck : IHealthCheck
{
    private readonly RabbitMqBusStartupStatus _status;

    public RabbitMqBusHealthCheck(RabbitMqBusStartupStatus status)
    {
        _status = status;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        return _status.Ready.IsCompletedSuccessfully
            ? Task.FromResult(HealthCheckResult.Healthy("RabbitMQ bus started."))
            : Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ bus is starting."));
    }
}
