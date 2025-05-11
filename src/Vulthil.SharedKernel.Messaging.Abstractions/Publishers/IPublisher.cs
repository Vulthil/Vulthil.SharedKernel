namespace Vulthil.SharedKernel.Messaging.Abstractions.Publishers;

public interface IPublisher
{
    Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
                where TMessage : class;
}
