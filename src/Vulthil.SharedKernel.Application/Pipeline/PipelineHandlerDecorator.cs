using Vulthil.SharedKernel.Application.Messaging;

namespace Vulthil.SharedKernel.Application.Pipeline;

/// <summary>
/// Decorates a registered handler with all <see cref="IPipelineHandler{TRequest, TResponse}"/>
/// behaviors resolved from the service provider at construction time.
/// </summary>
/// <remarks>
/// This is the single object that implements the request pipeline. It is registered against
/// every handler interface a concrete handler implements (<see cref="IHandler{TRequest, TResponse}"/>,
/// <see cref="ICommandHandler{TCommand, TResponse}"/>, <see cref="ICommandHandler{TCommand}"/>,
/// <see cref="IQueryHandler{TQuery, TResponse}"/>) via lightweight forwarding adapters so that
/// every direct injection and the <see cref="ISender"/> dispatch path share the same pipeline.
/// </remarks>
internal sealed class PipelineHandlerDecorator<TRequest, TResponse>(
    IInnerHandler<TRequest, TResponse> inner,
    IEnumerable<IPipelineHandler<TRequest, TResponse>> behaviors)
    : IHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IInnerHandler<TRequest, TResponse> _inner = inner;
    private readonly IPipelineHandler<TRequest, TResponse>[] _reversedBehaviors = behaviors.Reverse().ToArray();

    /// <inheritdoc />
    public Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        Task<TResponse> Handler(CancellationToken t) => _inner.HandleAsync(request, t);

        PipelineDelegate<TResponse> pipeline = Handler;
        foreach (var behavior in _reversedBehaviors)
        {
            var next = pipeline;
            pipeline = t => behavior.HandleAsync(request, next, t);
        }

        return pipeline(cancellationToken);
    }
}
