using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shipments.Application.Retention;
using Shipments.Domain;
using Shipments.Infrastructure;
using Shipments.Tests.Fixtures;
using Shouldly;

namespace Shipments.Tests;

public sealed class DeliveredShipmentArchiveTests(HarnessWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : HarnessTestCase(factory, testOutputHelper)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DeleteDeliveredAsync_deletes_only_delivered_shipments_older_than_the_cutoff()
    {
        // Arrange
        var oldDelivered = await AddShipmentAsync(ShipmentStatus.Delivered, Now.AddDays(-40));
        var recentDelivered = await AddShipmentAsync(ShipmentStatus.Delivered, Now.AddDays(-1));
        var inTransit = await AddShipmentAsync(ShipmentStatus.InTransit, Now.AddDays(-40));
        var archive = ScopedServices.GetRequiredService<DeliveredShipmentArchive>();

        // Act
        var deleted = await archive.DeleteDeliveredAsync(Now.AddDays(-30), batchSize: 10, CancellationToken);

        // Assert
        deleted.ShouldBe(1);
        var remaining = await ScopedServices.GetRequiredService<ShipmentsDbContext>().Shipments
            .AsNoTracking()
            .Select(shipment => shipment.Id)
            .ToListAsync(CancellationToken);
        remaining.ShouldNotContain(oldDelivered);
        remaining.ShouldContain(recentDelivered);
        remaining.ShouldContain(inTransit);
    }

    private async Task<ShipmentId> AddShipmentAsync(ShipmentStatus status, DateTimeOffset scannedAtUtc)
    {
        var shipment = Shipment.Create(Guid.NewGuid(), "Ada", "acme-express", scannedAtUtc.AddDays(-1), null).Value;
        shipment.RecordTrackingUpdate(status, "Hamburg", scannedAtUtc);

        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShipmentsDbContext>();
        dbContext.Shipments.Add(shipment);
        await dbContext.SaveChangesAsync(CancellationToken);
        return shipment.Id;
    }
}
