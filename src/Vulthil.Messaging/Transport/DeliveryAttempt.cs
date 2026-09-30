namespace Vulthil.Messaging.Transport;

/// <summary>
/// The result of one handler attempt. A failed attempt keeps the exception and a way to publish its fault, because
/// only the dispatcher knows — from the handler's policy — whether the failure is final.
/// </summary>
internal sealed class DeliveryAttempt
{
    private DeliveryAttempt(DeliveryAttemptResult result, Exception? exception, Func<IDeliveryPort, Task>? publishFault)
    {
        Result = result;
        Exception = exception;
        PublishFault = publishFault;
    }

    /// <summary>The consumer completed; for a request consumer, its response was sent.</summary>
    public static DeliveryAttempt Consumed { get; } = new(DeliveryAttemptResult.Consumed, null, null);

    /// <summary>A filter completed a one-way consumer's pipeline without calling the consumer.</summary>
    public static DeliveryAttempt ShortCircuited { get; } = new(DeliveryAttemptResult.ShortCircuited, null, null);

    /// <summary>A request consumer failed or produced no response, and an <see cref="RpcFault"/> reply was sent.</summary>
    public static DeliveryAttempt AnsweredWithFault { get; } = new(DeliveryAttemptResult.AnsweredWithFault, null, null);

    public DeliveryAttemptResult Result { get; }

    public Exception? Exception { get; }

    public Func<IDeliveryPort, Task>? PublishFault { get; }

    /// <summary>A one-way consumer threw <paramref name="exception"/>.</summary>
    public static DeliveryAttempt Failed(Exception exception, Func<IDeliveryPort, Task> publishFault)
        => new(DeliveryAttemptResult.Failed, exception, publishFault);
}

internal enum DeliveryAttemptResult
{
    Consumed,
    ShortCircuited,
    AnsweredWithFault,
    Failed,
}
