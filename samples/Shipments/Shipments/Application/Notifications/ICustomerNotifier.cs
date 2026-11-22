namespace Shipments.Application.Notifications;

/// <summary>
/// Tells the recipient about their shipment. The tests replace it with an in-memory double that the test framework
/// resets after each test.
/// </summary>
public interface ICustomerNotifier
{
    Task NotifyDeliveredAsync(DeliveryNotification notification, CancellationToken cancellationToken);
}
