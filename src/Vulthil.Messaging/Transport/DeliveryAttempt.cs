namespace Vulthil.Messaging.Transport;

/// <summary>
/// The result of one handler attempt. An attempt is not consumed when a filter completed the pipeline without calling
/// the consumer, when a request consumer answered with an <see cref="RpcFault"/>, or when it failed. A failed attempt
/// carries its <see cref="DeliveryFailure"/>, because only the dispatcher knows — from the handler's policy — whether
/// the failure is final.
/// </summary>
internal sealed record DeliveryAttempt(bool IsConsumed, DeliveryFailure? Failure)
{
    public static DeliveryAttempt Consumed { get; } = new(IsConsumed: true, Failure: null);

    public static DeliveryAttempt NotConsumed { get; } = new(IsConsumed: false, Failure: null);

    public static DeliveryAttempt Failed(Exception exception, Func<IDeliveryPort, Task> publishFault)
        => new(IsConsumed: false, new DeliveryFailure(exception, publishFault));
}
