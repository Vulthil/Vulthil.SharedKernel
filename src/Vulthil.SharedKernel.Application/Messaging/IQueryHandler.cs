namespace Vulthil.SharedKernel.Application.Messaging;

/// <summary>
/// Defines a handler for a query that returns a response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TQuery">The type of query to handle.</typeparam>
/// <typeparam name="TResponse">The type of response produced.</typeparam>
public interface IQueryHandler<TQuery, TResponse>
    : IHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>;
