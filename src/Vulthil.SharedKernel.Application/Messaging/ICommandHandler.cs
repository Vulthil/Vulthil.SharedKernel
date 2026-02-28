using Vulthil.Results;

namespace Vulthil.SharedKernel.Application.Messaging;

/// <summary>
/// Defines a handler for a command that returns a <see cref="Result"/>.
/// </summary>
/// <typeparam name="TCommand">The type of command to handle.</typeparam>
public interface ICommandHandler<TCommand>
    : ICommandHandler<TCommand, Result>
    where TCommand : ICommand;

/// <summary>
/// Defines a handler for a command that returns a response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TCommand">The type of command to handle.</typeparam>
/// <typeparam name="TResponse">The type of response produced.</typeparam>
public interface ICommandHandler<TCommand, TResponse>
    : IHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>;
