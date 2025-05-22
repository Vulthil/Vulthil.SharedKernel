using Vulthil.Results;

namespace Vulthil.SharedKernel.Application.Messaging;

public interface ICommandHandler<in TCommand>
    where TCommand : class, ICommand
{
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : class, ICommand<TResponse>
    where TResponse : class
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}
