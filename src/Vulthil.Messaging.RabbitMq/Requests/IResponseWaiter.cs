namespace Vulthil.Messaging.RabbitMq.Requests;

internal interface IResponseWaiter
{
    void Complete(ReadOnlySpan<byte> body);
}
