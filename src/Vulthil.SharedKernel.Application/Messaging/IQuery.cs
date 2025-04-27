using MediatR;
using Vulthil.Framework.Results.Results;

namespace Vulthil.SharedKernel.Application.Messaging;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
