namespace Shipments.Application.Carrier;

/// <summary>
/// Settings of the simulated carrier, bound from the <c>Carrier</c> configuration section.
/// </summary>
public sealed class CarrierOptions
{
    public const string SectionName = "Carrier";

    public string Name { get; set; } = "acme-express";

    public TimeSpan EstimateTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan TransitTime { get; set; } = TimeSpan.FromDays(2);

    public bool SimulatorEnabled { get; set; } = true;

    public TimeSpan SimulatorInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int SimulatorBatchSize { get; set; } = 50;
}
