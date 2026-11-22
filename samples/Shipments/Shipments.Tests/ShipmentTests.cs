using Shipments.Domain;
using Shipments.Domain.Events;
using Shouldly;
using Vulthil.xUnit;

namespace Shipments.Tests;

public sealed class ShipmentTests : BaseUnitTestCase
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_raises_ShipmentCreatedDomainEvent()
    {
        // Arrange
        var orderReference = Guid.NewGuid();

        // Act
        var result = Shipment.Create(orderReference, "  Ada Lovelace ", "acme-express", Now, null);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Recipient.ShouldBe("Ada Lovelace");
        result.Value.Status.ShouldBe(ShipmentStatus.Created);
        result.Value.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ShipmentCreatedDomainEvent>().OrderReference.ShouldBe(orderReference);
    }

    [Theory]
    [InlineData("", "Shipment.RecipientRequired")]
    [InlineData("   ", "Shipment.RecipientRequired")]
    public void Create_without_recipient_fails(string recipient, string expectedCode)
    {
        // Act
        var result = Shipment.Create(Guid.NewGuid(), recipient, "acme-express", Now, null);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(expectedCode);
    }

    [Fact]
    public void Create_without_order_reference_fails_before_checking_the_recipient()
    {
        // Act
        var result = Shipment.Create(Guid.Empty, string.Empty, "acme-express", Now, null);

        // Assert
        result.Error.ShouldBe(ShipmentErrors.OrderReferenceRequired);
    }

    [Fact]
    public void RecordTrackingUpdate_to_delivered_raises_ShipmentDeliveredDomainEvent()
    {
        // Arrange
        var shipment = Shipment.Create(Guid.NewGuid(), "Ada", "acme-express", Now, null).Value;
        shipment.ClearDomainEvents();

        // Act
        var pickedUp = shipment.RecordTrackingUpdate(ShipmentStatus.PickedUp, "Copenhagen", Now.AddHours(1));
        var delivered = shipment.RecordTrackingUpdate(ShipmentStatus.Delivered, "Hamburg", Now.AddHours(2));

        // Assert
        pickedUp.IsSuccess.ShouldBeTrue();
        delivered.IsSuccess.ShouldBeTrue();
        shipment.DeliveredAtUtc.ShouldBe(Now.AddHours(2));
        shipment.TrackingEvents.Select(trackingEvent => trackingEvent.Status).ShouldBe([ShipmentStatus.PickedUp, ShipmentStatus.Delivered]);
        shipment.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ShipmentDeliveredDomainEvent>();
    }

    [Fact]
    public void RecordTrackingUpdate_that_moves_backwards_fails_and_changes_nothing()
    {
        // Arrange
        var shipment = Shipment.Create(Guid.NewGuid(), "Ada", "acme-express", Now, null).Value;
        shipment.RecordTrackingUpdate(ShipmentStatus.InTransit, "Hamburg", Now.AddHours(1));

        // Act
        var result = shipment.RecordTrackingUpdate(ShipmentStatus.PickedUp, "Copenhagen", Now.AddHours(2));

        // Assert
        result.Error.Code.ShouldBe("Shipment.StatusOutOfOrder");
        shipment.Status.ShouldBe(ShipmentStatus.InTransit);
        shipment.TrackingEvents.ShouldHaveSingleItem();
    }
}
