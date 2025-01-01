using MediatR;
using Vulthil.SharedKernel.Primitives;

namespace Vulthil.SharedKernel.Application.Messaging;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
