using Vulthil.Results;

namespace Vulthil.SharedKernel.Application.Messaging;

public interface ITransactionalCommand : ITransactionalCommand<Result>;
public interface ITransactionalCommand<out TResponse> : ICommand<TResponse>;

public interface ICommand : ICommand<Result>, IBaseCommand;

public interface ICommand<out TResponse> : IHaveResponse<TResponse>, IBaseCommand;

public interface IBaseCommand;

public interface IHaveResponse<out TResponse>;
