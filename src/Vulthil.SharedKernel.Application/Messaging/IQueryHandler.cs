using Vulthil.Results;

namespace Vulthil.SharedKernel.Application.Messaging;

public interface IQueryHandler<TQuery, TResponse>
    : IHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>;

public interface IHandler<TRequest, TResponse>
    where TRequest : IHaveResponse<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TRequest command, CancellationToken cancellationToken = default);
}
public interface IHandler<TRequest>
    where TRequest : IHaveResponse
{
    Task<Result> HandleAsync(TRequest command, CancellationToken cancellationToken = default);
}
