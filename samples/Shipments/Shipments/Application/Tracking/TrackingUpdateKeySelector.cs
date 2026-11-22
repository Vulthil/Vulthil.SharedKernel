using Shipments.Messages;
using Vulthil.Messaging.Abstractions.Consumers;
using Vulthil.Messaging.Inbox;

namespace Shipments.Application.Tracking;

/// <summary>
/// The inbox key of a tracking update. The carrier resends a scan with a new message id when it gets no
/// acknowledgement, so the inbox deduplicates on the business identity of the scan instead: one key per shipment and
/// kind of scan.
/// </summary>
public sealed class TrackingUpdateKeySelector : IInboxKeySelector<TrackingUpdate>
{
    public string? GetKey(IMessageContext<TrackingUpdate> context) =>
        $"{context.Message.ShipmentId:N}:{context.Message.GetType().Name}";
}
