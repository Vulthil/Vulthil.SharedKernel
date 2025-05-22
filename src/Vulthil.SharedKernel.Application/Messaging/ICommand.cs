namespace Vulthil.SharedKernel.Application.Messaging;

public interface ITransactionalCommand : ICommand;
public interface ITransactionalCommand<TResponse> : ICommand<TResponse>
    where TResponse : class;

public interface ICommand : IBaseCommand;

public interface ICommand<TResponse> : IBaseCommand
    where TResponse : class;

public interface IBaseCommand;
