namespace Shipments.Application.Notifications;

internal sealed class LoggingCustomerNotifier(ILogger<LoggingCustomerNotifier> logger) : ICustomerNotifier
{
    public Task NotifyDeliveredAsync(DeliveryNotification notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Telling {Recipient} that shipment {ShipmentId} was delivered at {DeliveredAtUtc}",
            notification.Recipient,
            notification.ShipmentId,
            notification.DeliveredAtUtc);
        return Task.CompletedTask;
    }
}
