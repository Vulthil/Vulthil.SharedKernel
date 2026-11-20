namespace Vulthil.Messaging.Transport;

/// <summary>
/// A failed handler attempt: the exception the consumer threw, and how to publish its fault once the dispatcher decides
/// the failure is final.
/// </summary>
internal sealed record DeliveryFailure(Exception Exception, Func<IDeliveryPort, Task> PublishFault);
