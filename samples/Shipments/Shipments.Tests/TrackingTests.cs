using System.Collections.Concurrent;
using Shipments.Application.Shipments;
using Shipments.Domain;
using Shipments.Messages;
using Shipments.Tests.Fixtures;
using Shouldly;
using Vulthil.Extensions.Testing;
using Vulthil.Messaging.Abstractions.Consumers;

namespace Shipments.Tests;

public sealed class TrackingTests(HarnessWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : HarnessTestCase(factory, testOutputHelper)
{
    private static readonly DateTimeOffset Scanned = new(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Scans_deliver_the_shipment_and_publish_ShipmentDelivered_to_subscribers()
    {
        // Arrange
        var shipment = await CreateShipmentAsync();
        var downstream = new ConcurrentQueue<IMessageContext<ShipmentDelivered>>();
        Harness.Handle<ShipmentDelivered>(context =>
        {
            downstream.Enqueue(context);
            return Task.CompletedTask;
        });

        // Act
        await Publisher.PublishAsync(TrackingUpdates.PickedUp(shipment.Id, "Copenhagen", Scanned), CancellationToken);
        await Publisher.PublishAsync(TrackingUpdates.InTransit(shipment.Id, "Hamburg", Scanned.AddHours(4)), CancellationToken);
        await Publisher.PublishAsync(TrackingUpdates.Delivered(shipment.Id, "Amsterdam", Scanned.AddHours(9)), CancellationToken);

        // Assert
        var delivered = await GetShipmentAsync(shipment.Id);
        delivered.Status.ShouldBe(ShipmentStatus.Delivered);
        delivered.DeliveredAtUtc.ShouldBe(Scanned.AddHours(9));
        delivered.TrackingEvents.Select(e => e.Status).ShouldBe([ShipmentStatus.PickedUp, ShipmentStatus.InTransit, ShipmentStatus.Delivered]);

        var relayed = await Polling.WaitAsync(
            new PollingOptions(TimeSpan.FromSeconds(20)) { TimerTick = TimeSpan.FromMilliseconds(250) },
            _ => Task.FromResult(downstream.Any(context => context.Message.ShipmentId == shipment.Id)),
            CancellationToken);
        relayed.IsSuccess.ShouldBeTrue();

        var message = downstream.Single(context => context.Message.ShipmentId == shipment.Id);
        message.Message.TrackingEventCount.ShouldBe(3);
        message.CorrelationId.ShouldBe(shipment.OrderReference.ToString());
        CustomerNotifier.Sent.ShouldContain(notification => notification.ShipmentId == shipment.Id && notification.Recipient == "Ada");
    }

    [Fact]
    public async Task Scan_resent_with_a_new_message_id_is_recorded_once()
    {
        // Arrange
        var shipment = await CreateShipmentAsync();
        var scan = TrackingUpdates.PickedUp(shipment.Id, "Copenhagen", Scanned);

        // Act
        await Publisher.PublishAsync(scan, CancellationToken);
        await Publisher.PublishAsync(scan, CancellationToken);

        // Assert
        var shipmentAfterScans = await GetShipmentAsync(shipment.Id);
        shipmentAfterScans.TrackingEvents.ShouldHaveSingleItem().Status.ShouldBe(ShipmentStatus.PickedUp);
    }

    [Fact]
    public async Task Scan_for_an_unknown_shipment_publishes_a_fault_without_retrying()
    {
        // Arrange
        var unknownShipmentId = Guid.NewGuid();

        // Act
        await Publisher.PublishAsync(TrackingUpdates.PickedUp(unknownShipmentId, "Copenhagen", Scanned), CancellationToken);

        // Assert
        var fault = Harness.Published<Fault<TrackingUpdate>>().ShouldHaveSingleItem().Message;
        fault.Message.ShipmentId.ShouldBe(unknownShipmentId);
        fault.ExceptionType.ShouldContain(nameof(ShipmentNotFoundException));
        fault.OriginalContext.RetryCount.ShouldBe(0);
    }

    private async Task<ShipmentDto> CreateShipmentAsync()
    {
        var orderReference = Guid.NewGuid();
        Harness.Respond<GetDeliveryEstimate, DeliveryEstimate>(_ => new DeliveryEstimate(Scanned.AddDays(2)));
        await SendCreateShipmentAsync(new CreateShipment(orderReference, "Ada"));

        var response = await Client.GetAsync($"shipments?orderReference={orderReference}", CancellationToken);
        var shipments = await response.GetResponseAsync<List<ShipmentDto>>(CancellationToken);
        return shipments.ShouldHaveSingleItem();
    }

    private async Task<ShipmentDto> GetShipmentAsync(Guid id)
    {
        var response = await Client.GetAsync($"shipments/{id}", CancellationToken);
        return await response.GetResponseAsync<ShipmentDto>(CancellationToken);
    }
}
