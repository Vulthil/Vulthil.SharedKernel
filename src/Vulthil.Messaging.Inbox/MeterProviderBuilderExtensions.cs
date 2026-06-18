using OpenTelemetry.Metrics;

namespace Vulthil.Messaging.Inbox;

/// <summary>
/// Extension methods for subscribing a <see cref="MeterProviderBuilder"/> to the inbox metrics.
/// </summary>
public static class MeterProviderBuilderExtensions
{
    /// <summary>
    /// Subscribes the meter provider to the <see cref="System.Diagnostics.Metrics.Meter"/> emitted by the inbox
    /// guard (the <c>vulthil.inbox.*</c> counters), so inbox metrics are collected without hard-coding the meter name.
    /// </summary>
    /// <param name="builder">The meter provider builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static MeterProviderBuilder AddVulthilInboxInstrumentation(this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddMeter(InboxTelemetry.MeterName);
    }
}
