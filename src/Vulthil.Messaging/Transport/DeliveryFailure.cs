namespace Vulthil.Messaging.Transport;

internal sealed record DeliveryFailure(Exception Exception, Func<IDeliveryPort, Task> PublishFault);
