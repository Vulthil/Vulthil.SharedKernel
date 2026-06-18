using System.Diagnostics.Metrics;

namespace Vulthil.Messaging.Inbox;

/// <summary>
/// Holds the <see cref="System.Diagnostics.Metrics.Meter"/> and instruments for inbox idempotency metrics.
/// </summary>
public static class InboxTelemetry
{
    /// <summary>
    /// Meter name for inbox metrics. Subscribe to it on a <c>MeterProviderBuilder</c> with
    /// <c>AddVulthilInboxInstrumentation()</c>.
    /// </summary>
    public static readonly string MeterName = "Vulthil.Messaging.Inbox";
    internal static readonly Meter Meter = new(MeterName);
    internal static readonly Counter<long> Processed = Meter.CreateCounter<long>(
        "vulthil.inbox.processed", unit: "{message}", description: "Messages processed through the inbox guard on first delivery.");
    internal static readonly Counter<long> DuplicateSkipped = Meter.CreateCounter<long>(
        "vulthil.inbox.duplicate_skipped", unit: "{message}", description: "Duplicate deliveries skipped by the inbox guard.");
    internal static readonly Counter<long> MissingKey = Meter.CreateCounter<long>(
        "vulthil.inbox.missing_key", unit: "{message}", description: "Deliveries processed without an idempotency key.");
}
