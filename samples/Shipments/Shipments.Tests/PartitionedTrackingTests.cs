using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shipments.Domain;
using Shipments.Infrastructure;
using Shipments.Messages;
using Shipments.Tests.Fixtures;
using Shouldly;
using Vulthil.Extensions.Testing;
using Vulthil.Results;

namespace Shipments.Tests;

/// <summary>
/// Runs on the real broker. A shipment only moves forward, so a scan applied out of order is skipped and the shipment
/// ends with fewer than three tracking events; three events per shipment prove the partitioner kept the order.
/// </summary>
public sealed class PartitionedTrackingTests(ShipmentsWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BrokerTestCase(factory, testOutputHelper)
{
    private static readonly DateTimeOffset Scanned = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Scans_of_each_shipment_are_applied_in_publish_order()
    {
        // Arrange
        await WaitForBrokerAsync();
        var shipmentIds = await AddShipmentsAsync(count: 8);

        // Act
        foreach (var shipmentId in shipmentIds)
        {
            await Publisher.PublishAsync(TrackingUpdates.PickedUp(shipmentId.Value, "Copenhagen", Scanned), CancellationToken);
            await Publisher.PublishAsync(TrackingUpdates.InTransit(shipmentId.Value, "Hamburg", Scanned.AddHours(1)), CancellationToken);
            await Publisher.PublishAsync(TrackingUpdates.Delivered(shipmentId.Value, "Amsterdam", Scanned.AddHours(2)), CancellationToken);
        }

        // Assert
        var result = await Polling.WaitAsync(TimeSpan.FromSeconds(30), async cancellationToken =>
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var shipments = await scope.ServiceProvider.GetRequiredService<ShipmentsDbContext>().Shipments
                .AsNoTracking()
                .Include(shipment => shipment.TrackingEvents)
                .Where(shipment => shipmentIds.Contains(shipment.Id))
                .ToListAsync(cancellationToken);

            return shipments.Count == shipmentIds.Count && shipments.All(shipment => shipment.Status == ShipmentStatus.Delivered)
                ? Result.Success(shipments)
                : Result.Failure<List<Shipment>>(Error.Failure("Shipments.NotDelivered", "Not every shipment is delivered yet."));
        }, CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldAllBe(shipment => shipment.TrackingEvents.Count == 3);
    }

    private async Task<List<ShipmentId>> AddShipmentsAsync(int count)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShipmentsDbContext>();

        var shipments = Enumerable.Range(0, count)
            .Select(i => Shipment.Create(Guid.NewGuid(), $"Recipient {i}", "acme-express", Scanned.AddDays(-1), null).Value)
            .ToList();
        dbContext.Shipments.AddRange(shipments);
        await dbContext.SaveChangesAsync(CancellationToken);

        return [.. shipments.Select(shipment => shipment.Id)];
    }
}
