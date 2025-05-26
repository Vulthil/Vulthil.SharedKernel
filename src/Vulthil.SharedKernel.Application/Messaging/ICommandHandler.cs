namespace Vulthil.SharedKernel.Application.Messaging;

public interface ICommandHandler<TCommand>
    : IHandler<TCommand>
    where TCommand : ICommand;

public interface ICommandHandler<TCommand, TResponse>
    : IHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>;
