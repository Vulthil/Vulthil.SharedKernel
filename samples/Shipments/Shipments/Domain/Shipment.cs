using Shipments.Domain.Events;
using Vulthil.Results;
using Vulthil.SharedKernel.Primitives;

namespace Shipments.Domain;

public sealed class Shipment : AggregateRoot<ShipmentId>
{
    private readonly List<TrackingEvent> _trackingEvents = [];

    private Shipment(
        ShipmentId id,
        Guid orderReference,
        string recipient,
        string carrier,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? estimatedDeliveryUtc)
        : base(id)
    {
        OrderReference = orderReference;
        Recipient = recipient;
        Carrier = carrier;
        Status = ShipmentStatus.Created;
        CreatedAtUtc = createdAtUtc;
        EstimatedDeliveryUtc = estimatedDeliveryUtc;
    }

    public Guid OrderReference { get; private set; }

    public string Recipient { get; private set; }

    public string Carrier { get; private set; }

    public ShipmentStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? EstimatedDeliveryUtc { get; private set; }

    public DateTimeOffset? DeliveredAtUtc { get; private set; }

    public IReadOnlyList<TrackingEvent> TrackingEvents => _trackingEvents;

    public static Result<Shipment> Create(
        Guid orderReference,
        string recipient,
        string carrier,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? estimatedDeliveryUtc) =>
        Result.Success(orderReference)
            .Ensure(reference => reference != Guid.Empty, ShipmentErrors.OrderReferenceRequired)
            .Ensure(_ => !string.IsNullOrWhiteSpace(recipient), ShipmentErrors.RecipientRequired)
            .Map(reference => new Shipment(ShipmentId.New(), reference, recipient.Trim(), carrier, createdAtUtc, estimatedDeliveryUtc))
            .Tap(shipment => shipment.Raise(new ShipmentCreatedDomainEvent(shipment.Id, shipment.OrderReference)));

    public Result RescheduleDelivery(DateTimeOffset estimatedDeliveryUtc)
    {
        if (Status is ShipmentStatus.Delivered)
        {
            return Result.Failure(ShipmentErrors.AlreadyDelivered);
        }

        EstimatedDeliveryUtc = estimatedDeliveryUtc;
        return Result.Success();
    }

    /// <summary>
    /// Records a carrier scan. A shipment only moves forward, so a scan for a status the shipment has already reached
    /// fails instead of rewriting its history; the carrier resends scans, and a resent scan must change nothing.
    /// </summary>
    public Result RecordTrackingUpdate(ShipmentStatus status, string location, DateTimeOffset occurredAtUtc)
    {
        if (status <= Status)
        {
            return Result.Failure(ShipmentErrors.StatusOutOfOrder(Status, status));
        }

        _trackingEvents.Add(TrackingEvent.Create(status, location, occurredAtUtc));
        Status = status;

        if (status is ShipmentStatus.Delivered)
        {
            DeliveredAtUtc = occurredAtUtc;
            Raise(new ShipmentDeliveredDomainEvent(Id, occurredAtUtc));
        }

        return Result.Success();
    }
}
