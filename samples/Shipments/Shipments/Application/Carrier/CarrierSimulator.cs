using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shipments.Domain;
using Shipments.Infrastructure;
using Shipments.Messages;
using Vulthil.Extensions.Hosting;
using Vulthil.Messaging.Abstractions.Publishers;

namespace Shipments.Application.Carrier;

/// <summary>
/// Plays the carrier: on every tick it publishes the next scan of each shipment that is not delivered yet. It derives
/// from <see cref="RestartableBackgroundService"/>, so the test framework can pause it around a database reset and
/// start it again afterwards. A singleton cannot hold the scoped <see cref="IPublisher"/>, so every tick resolves one
/// from a scope of its own.
/// </summary>
/// <remarks>
/// A tick can publish a scan again before the previous one was applied. That is how a real carrier behaves, and the
/// inbox and the shipment's own status rules make the repeat harmless.
/// </remarks>
public sealed class CarrierSimulator(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<CarrierOptions> options,
    TimeProvider timeProvider,
    IHostApplicationLifetime applicationLifetime,
    ILogger<CarrierSimulator> logger) : RestartableBackgroundService(applicationLifetime, logger)
{
    private static readonly string[] Hubs = ["Copenhagen", "Hamburg", "Amsterdam", "Brussels"];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.CurrentValue.SimulatorInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!options.CurrentValue.SimulatorEnabled)
            {
                continue;
            }

            try
            {
                await PublishNextScansAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                // One failed tick, for example while the broker restarts, must not stop the simulator: the next tick
                // publishes the same scans again.
                Logger.LogWarning(exception, "The carrier simulator could not publish this round of scans");
            }
        }
    }

    private async Task PublishNextScansAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShipmentsDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var activeShipments = await dbContext.Shipments
            .AsNoTracking()
            .Where(shipment => shipment.Status != ShipmentStatus.Delivered)
            .OrderBy(shipment => shipment.CreatedAtUtc)
            .Take(options.CurrentValue.SimulatorBatchSize)
            .Select(shipment => new { shipment.Id, shipment.Status })
            .ToListAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        foreach (var shipment in activeShipments)
        {
            var location = Hubs[(int)shipment.Status % Hubs.Length];
            TrackingUpdate scan = shipment.Status switch
            {
                ShipmentStatus.Created => TrackingUpdates.PickedUp(shipment.Id.Value, location, now),
                ShipmentStatus.PickedUp => TrackingUpdates.InTransit(shipment.Id.Value, location, now),
                _ => TrackingUpdates.Delivered(shipment.Id.Value, location, now),
            };

            await publisher.PublishAsync(scan, cancellationToken);
        }
    }
}
