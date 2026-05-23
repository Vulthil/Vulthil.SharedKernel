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
        var ready = _status.Ready;

        if (ready.IsCompletedSuccessfully)
        {
            return Task.FromResult(HealthCheckResult.Healthy("RabbitMQ bus started."));
        }

        if (ready.IsFaulted)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "RabbitMQ bus failed to start.",
                ready.Exception?.GetBaseException()));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ bus is starting."));
    }
}
