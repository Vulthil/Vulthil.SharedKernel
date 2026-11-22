namespace Shipments.Application.Notifications;

public sealed record DeliveryNotification(Guid ShipmentId, string Recipient, DateTimeOffset DeliveredAtUtc);
