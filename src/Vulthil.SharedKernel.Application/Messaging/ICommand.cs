using Vulthil.Results;

namespace Vulthil.SharedKernel.Application.Messaging;

/// <summary>
/// Marker interface for a command that returns a <see cref="Result"/>.
/// </summary>
public interface ICommand : ICommand<Result>;
/// <summary>
/// Marker interface for a command that returns a response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TResponse">The type of response produced by this command.</typeparam>
public interface ICommand<out TResponse> : IRequest<TResponse>;

/// <summary>
/// Marker interface for a command that should be executed within a transaction.
/// </summary>
public interface ITransactionalCommand : ICommand;
/// <summary>
/// Marker interface for a transactional command that returns a response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TResponse">The type of response produced by this command.</typeparam>
public interface ITransactionalCommand<out TResponse> : ICommand<TResponse>;
