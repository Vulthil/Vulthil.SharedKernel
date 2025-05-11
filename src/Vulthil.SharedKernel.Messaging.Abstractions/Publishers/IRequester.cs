using Vulthil.Framework.Results.Results;

namespace Vulthil.SharedKernel.Messaging.Abstractions.Publishers;

public interface IRequester
{
    Task<Result<TResponse>> RequestAsync<TRequest, TResponse>(TRequest message, CancellationToken cancellationToken = default)
                    where TRequest : class
                    where TResponse : class;
}
