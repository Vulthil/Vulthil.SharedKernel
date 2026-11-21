namespace Shipments.Application.Retention;

/// <summary>
/// How long delivered shipments are kept, bound from <c>Shipments:Retention</c>. The on/off switch lives here and not
/// on the retention sweep: the sweep is only registered when <see cref="Enabled"/> is set.
/// </summary>
public sealed class ShipmentRetentionOptions
{
    public const string SectionName = "Shipments:Retention";

    public bool Enabled { get; set; }

    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(30);

    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromHours(6);

    public int BatchSize { get; set; } = 500;
}
