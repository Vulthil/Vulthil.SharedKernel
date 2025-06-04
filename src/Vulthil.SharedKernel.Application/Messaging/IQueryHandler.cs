namespace Vulthil.SharedKernel.Application.Messaging;

public interface IQueryHandler<TQuery, TResponse>
    : IHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>;

public interface IHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}
