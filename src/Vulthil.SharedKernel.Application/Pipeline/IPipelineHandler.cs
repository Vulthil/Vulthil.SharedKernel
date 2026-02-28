using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Events;

namespace Vulthil.SharedKernel.Application.Pipeline;

/// <summary>
/// Represents the next step in the request pipeline.
/// </summary>
/// <typeparam name="TResponse">The type of response produced by the pipeline.</typeparam>
/// <param name="cancellationToken">A token to observe for cancellation.</param>
public delegate Task<TResponse> PipelineDelegate<TResponse>(CancellationToken cancellationToken = default);

/// <summary>
/// Defines a pipeline handler that wraps request processing with cross-cutting behavior.
/// </summary>
/// <typeparam name="TRequest">The type of request being processed.</typeparam>
/// <typeparam name="TResponse">The type of response produced.</typeparam>
public interface IPipelineHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the request, optionally invoking the next step in the pipeline.
    /// </summary>
    /// <param name="request">The request being processed.</param>
    /// <param name="next">The next step in the pipeline.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task containing the response.</returns>
    Task<TResponse> HandleAsync(TRequest request, PipelineDelegate<TResponse> next, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the next step in the domain event pipeline.
/// </summary>
/// <param name="cancellationToken">A token to observe for cancellation.</param>
public delegate Task DomainEventPipelineDelegate(CancellationToken cancellationToken = default);

/// <summary>
/// Defines a pipeline handler that wraps domain event processing with cross-cutting behavior.
/// </summary>
/// <typeparam name="TDomainEvent">The type of domain event being processed.</typeparam>
public interface IDomainEventPipelineHandler<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    /// <summary>
    /// Handles the domain event, optionally invoking the next step in the pipeline.
    /// </summary>
    /// <param name="domainEvent">The domain event being processed.</param>
    /// <param name="next">The next step in the pipeline.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleAsync(TDomainEvent domainEvent, DomainEventPipelineDelegate next, CancellationToken cancellationToken = default);
}
