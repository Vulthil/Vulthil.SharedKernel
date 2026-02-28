using Vulthil.Results;

namespace Vulthil.SharedKernel.Application.Messaging;

#pragma warning disable S2326 // Unused type parameters should be removed
/// <summary>
/// Marker interface for a request that produces a response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TResponse">The type of response produced by this request.</typeparam>
public interface IRequest<out TResponse>;
#pragma warning restore S2326 // Unused type parameters should be removed
/// <summary>
/// Marker interface for a request that produces a <see cref="Result"/>.
/// </summary>
public interface IRequest : IRequest<Result>;

