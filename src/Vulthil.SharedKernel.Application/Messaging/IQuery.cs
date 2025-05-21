using MediatR;
using Vulthil.Results;

namespace Vulthil.SharedKernel.Application.Messaging;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
