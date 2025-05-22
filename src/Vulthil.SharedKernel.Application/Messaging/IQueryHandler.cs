using Vulthil.Results;

namespace Vulthil.SharedKernel.Application.Messaging;

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : class, IQuery<TResponse>
    where TResponse : class
{
    Task<Result<TResponse>> HandleAsync(TQuery command, CancellationToken cancellationToken = default);
}
