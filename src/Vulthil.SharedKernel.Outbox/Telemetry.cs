using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Static class for holding the Telemetry ActivitySource used for all outbox processing operations, enabling consistent correlation of telemetry across the capture, processing, and publishing stages.
/// </summary>
public static class Telemetry
{
    /// <summary>
    /// ActivitySourceName for all outbox processing operations, allowing correlation of events across the capture, processing, and publishing stages.
    /// </summary>
    public static string ActivitySourceName => "Vulthil.SharedKernel.Outbox";
    /// <summary>
    /// ActivitySource for all outbox processing operations, allowing correlation of events across the capture, processing, and publishing stages.
    /// </summary>
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>
    /// Meter name for outbox relay metrics. Subscribe to it on a <c>MeterProviderBuilder</c> with
    /// <c>AddVulthilOutboxInstrumentation()</c>.
    /// </summary>
    public static string MeterName => "Vulthil.SharedKernel.Outbox";
    internal static readonly Meter Meter = new(MeterName);
    internal static readonly Counter<long> Relayed = Meter.CreateCounter<long>(
        "vulthil.outbox.relayed", unit: "{message}", description: "Outbox messages successfully relayed.");
    internal static readonly Counter<long> Failed = Meter.CreateCounter<long>(
        "vulthil.outbox.failed", unit: "{message}", description: "Outbox message relay attempts that failed.");
}
