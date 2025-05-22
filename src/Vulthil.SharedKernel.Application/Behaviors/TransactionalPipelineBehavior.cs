using Vulthil.Results;
using Vulthil.SharedKernel.Application.Data;
using Vulthil.SharedKernel.Application.Messaging;

namespace Vulthil.SharedKernel.Application.Behaviors;

public sealed class TransactionalPipelineBehavior<TCommand, TResponse>(
    IUnitOfWork unitOfWork,
    ICommandHandler<TCommand, TResponse> innerHandler)
    : ICommandHandler<TCommand, TResponse>
    where TCommand : class, ITransactionalCommand<TResponse>
    where TResponse : class
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICommandHandler<TCommand, TResponse> _innerHandler = innerHandler;

    public async Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var response = await _innerHandler.HandleAsync(command, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return response;
    }
}
public sealed class TransactionalPipelineBehavior<TCommand>(
    IUnitOfWork unitOfWork,
    ICommandHandler<TCommand> innerHandler)
    : ICommandHandler<TCommand>
    where TCommand : class, ITransactionalCommand
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICommandHandler<TCommand> _innerHandler = innerHandler;

    public async Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var response = await _innerHandler.HandleAsync(command, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return response;
    }
}

