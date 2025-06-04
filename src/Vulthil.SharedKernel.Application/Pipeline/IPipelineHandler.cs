using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Events;

namespace Vulthil.SharedKernel.Application.Pipeline;

public delegate Task<TResponse> PipelineDelegate<TResponse>(CancellationToken cancellationToken = default);

public interface IPipelineHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, PipelineDelegate<TResponse> next, CancellationToken cancellationToken = default);
}

public delegate Task DomainEventPipelineDelegate(CancellationToken cancellationToken = default);

public interface IDomainEventPipelineHandler<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    Task HandleAsync(TDomainEvent domainEvent, DomainEventPipelineDelegate next, CancellationToken cancellationToken = default);
}
