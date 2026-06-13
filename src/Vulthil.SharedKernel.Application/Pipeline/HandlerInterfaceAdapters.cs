using Vulthil.Results;
using Vulthil.SharedKernel.Application.Messaging;

namespace Vulthil.SharedKernel.Application.Pipeline;

/// <summary>
/// Forwards <see cref="ICommandHandler{TCommand, TResponse}"/> resolutions to the
/// pipeline-decorated <see cref="IHandler{TRequest, TResponse}"/> so direct injection
/// of <see cref="ICommandHandler{TCommand, TResponse}"/> shares the same pipeline as <see cref="ISender"/>.
/// </summary>
internal sealed class CommandHandlerAdapter<TCommand, TResponse>(IHandler<TCommand, TResponse> handler)
    : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private readonly IHandler<TCommand, TResponse> _handler = handler;

    /// <inheritdoc />
    public Task<TResponse> HandleAsync(TCommand request, CancellationToken cancellationToken = default) =>
        _handler.HandleAsync(request, cancellationToken);
}

/// <summary>
/// Forwards <see cref="ICommandHandler{TCommand}"/> (which produces a <see cref="Result"/>)
/// resolutions to the pipeline-decorated <see cref="IHandler{TRequest, TResponse}"/>.
/// </summary>
internal sealed class CommandHandlerUnitAdapter<TCommand>(IHandler<TCommand, Result> handler)
    : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    private readonly IHandler<TCommand, Result> _handler = handler;

    /// <inheritdoc />
    public Task<Result> HandleAsync(TCommand request, CancellationToken cancellationToken = default) =>
        _handler.HandleAsync(request, cancellationToken);
}

/// <summary>
/// Forwards <see cref="IQueryHandler{TQuery, TResponse}"/> resolutions to the
/// pipeline-decorated <see cref="IHandler{TRequest, TResponse}"/>.
/// </summary>
internal sealed class QueryHandlerAdapter<TQuery, TResponse>(IHandler<TQuery, TResponse> handler)
    : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    private readonly IHandler<TQuery, TResponse> _handler = handler;

    /// <inheritdoc />
    public Task<TResponse> HandleAsync(TQuery request, CancellationToken cancellationToken = default) =>
        _handler.HandleAsync(request, cancellationToken);
}
