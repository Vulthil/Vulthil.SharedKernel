using OpenTelemetry.Metrics;

namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Extension methods for subscribing a <see cref="MeterProviderBuilder"/> to the outbox relay metrics.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Subscribes the meter provider to the <see cref="System.Diagnostics.Metrics.Meter"/> emitted by the outbox
    /// relay (the <c>vulthil.outbox.*</c> counters), so outbox metrics are collected without hard-coding the meter name.
    /// </summary>
    /// <param name="builder">The meter provider builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static MeterProviderBuilder AddVulthilOutboxInstrumentation(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddMeter(Telemetry.MeterName);
    }
}
