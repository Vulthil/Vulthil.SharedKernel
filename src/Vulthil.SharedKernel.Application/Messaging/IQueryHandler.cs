using MediatR;
using Vulthil.SharedKernel.Primitives;

namespace Vulthil.SharedKernel.Application.Messaging;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
