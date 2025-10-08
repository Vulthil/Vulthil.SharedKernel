using Vulthil.Results;

namespace Vulthil.SharedKernel.Application.Messaging;

#pragma warning disable S2326 // Unused type parameters should be removed
public interface IRequest<out TResponse>;
#pragma warning restore S2326 // Unused type parameters should be removed
public interface IRequest : IRequest<Result>;

