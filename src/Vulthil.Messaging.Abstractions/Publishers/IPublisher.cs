namespace Vulthil.Messaging.Abstractions.Publishers;

public interface IPublisher
{
    Task PublishAsync<TMessage>(
        TMessage message,
        string? routingKey = null,
        CancellationToken cancellationToken = default)
        where TMessage : notnull;
}
