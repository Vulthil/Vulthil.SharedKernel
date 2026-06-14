using Vulthil.Results;
using Vulthil.SharedKernel.Application.Data;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Application.Pipeline;

namespace Vulthil.SharedKernel.Application.Behaviors;

/// <summary>
/// Pipeline behavior that wraps transactional commands in a database transaction, committing when the command
/// succeeds and rolling back when it returns a failed <see cref="Result"/> or throws.
/// </summary>
public sealed class TransactionalPipelineBehavior<TCommand, TResponse>(
    IUnitOfWork unitOfWork)
    : IPipelineHandler<TCommand, TResponse>
    where TCommand : ITransactionalCommand<TResponse>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    /// <inheritdoc />
    public Task<TResponse> HandleAsync(TCommand request, PipelineDelegate<TResponse> next, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(next);

        return _unitOfWork.ExecuteInTransactionAsync(token => next(token), static response => response is not Result { IsFailure: true }, cancellationToken);
    }
}

