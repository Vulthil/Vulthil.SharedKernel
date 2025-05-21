using MediatR;
using Vulthil.Results;

namespace Vulthil.SharedKernel.Application.Messaging;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
