namespace Vulthil.SharedKernel.Application.Messaging;

public interface IQueryHandler<TQuery, TResponse>
    : IHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>;

public interface IHandler<TRequest, TResponse>
    where TRequest : IHaveResponse<TResponse>
{
    Task<TResponse> HandleAsync(TRequest command, CancellationToken cancellationToken = default);
}
