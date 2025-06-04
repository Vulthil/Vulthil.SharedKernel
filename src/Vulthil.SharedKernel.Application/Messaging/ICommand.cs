using Vulthil.Results;

namespace Vulthil.SharedKernel.Application.Messaging;

public interface ICommand : ICommand<Result>;
public interface ICommand<out TResponse> : IRequest<TResponse>;

public interface ITransactionalCommand : ICommand;
public interface ITransactionalCommand<out TResponse> : ICommand<TResponse>;
