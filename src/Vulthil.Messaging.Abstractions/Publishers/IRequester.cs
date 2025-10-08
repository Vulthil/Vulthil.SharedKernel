using Vulthil.Results;

namespace Vulthil.Messaging.Abstractions.Publishers;

public interface IRequester
{
    Task<Result<TResponse>> RequestAsync<TRequest, TResponse>(TRequest message, CancellationToken cancellationToken = default)
                    where TRequest : notnull
                    where TResponse : notnull;
}
