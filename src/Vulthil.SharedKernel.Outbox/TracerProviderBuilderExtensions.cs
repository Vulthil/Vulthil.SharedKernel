using OpenTelemetry.Trace;

namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// OpenTelemetry registration helpers for the Vulthil outbox processing telemetry.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Subscribes the tracer provider to the <see cref="System.Diagnostics.ActivitySource"/> emitted by the outbox
    /// relay (the <c>OutboxPublishing</c> spans), so outbox processing is collected without hard-coding the source name.
    /// </summary>
    /// <param name="builder">The tracer provider builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static TracerProviderBuilder AddVulthilOutboxInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource(Telemetry.ActivitySourceName);
    }
}
