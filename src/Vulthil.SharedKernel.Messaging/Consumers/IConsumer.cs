namespace Vulthil.SharedKernel.Messaging.Consumers;

public interface IConsumer;
public interface IConsumer<in TMessage> : IConsumer
{
    Task ConsumeAsync(TMessage message, CancellationToken cancellationToken = default);
}
