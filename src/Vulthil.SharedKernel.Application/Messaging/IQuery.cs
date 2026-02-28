namespace Vulthil.SharedKernel.Application.Messaging;

/// <summary>
/// Marker interface for a query that returns a response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TResponse">The type of response produced by this query.</typeparam>
public interface IQuery<out TResponse> : IRequest<TResponse>;
