using Vulthil.Results;

namespace Vulthil.SharedKernel.Application.Messaging;

public interface ICommandHandler<TCommand>
    : ICommandHandler<TCommand, Result>
    where TCommand : ICommand;

public interface ICommandHandler<TCommand, TResponse>
    : IHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>;
