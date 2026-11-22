using OpenTelemetry.Trace;

namespace Vulthil.Messaging.RabbitMq;

/// <summary>
/// OpenTelemetry registration helpers for the Vulthil RabbitMQ transport.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Subscribes the tracer provider to the activity source emitted by the Vulthil RabbitMQ transport.
    /// </summary>
    /// <param name="builder">The tracer provider builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static TracerProviderBuilder AddVulthilRabbitMqInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource(RabbitMqTelemetry.ActivitySourceName);
    }
}
