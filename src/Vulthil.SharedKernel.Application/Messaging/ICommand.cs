namespace Vulthil.SharedKernel.Application.Messaging;

public interface ITransactionalCommand : ICommand;
public interface ITransactionalCommand<out TResponse> : ICommand<TResponse>;

public interface ICommand : IHaveResponse, IBaseCommand;

public interface ICommand<out TResponse> : IHaveResponse<TResponse>, IBaseCommand;

public interface IBaseCommand;

public interface IHaveResponse<out TResponse>;
public interface IHaveResponse;
