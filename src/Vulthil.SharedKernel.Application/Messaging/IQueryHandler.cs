namespace Vulthil.SharedKernel.Application.Messaging;

public interface IQueryHandler<TQuery, TResponse>
    : IHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>;
