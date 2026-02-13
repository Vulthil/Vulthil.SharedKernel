namespace Vulthil.Messaging.Abstractions.Consumers;

public interface IRequestConsumer;

public interface IRequestConsumer<in TRequest, TResponse> : IRequestConsumer
    where TRequest : notnull
    where TResponse : notnull
{
    Task<TResponse> ConsumeAsync(IMessageContext<TRequest> messageContext, CancellationToken cancellationToken = default);
}
