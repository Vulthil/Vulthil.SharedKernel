using Vulthil.SharedKernel.Application.Data;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.SharedKernel.Application.Pipeline;

namespace Vulthil.SharedKernel.Application.Behaviors;

public sealed class TransactionalPipelineBehavior<TCommand, TResponse>(
    IUnitOfWork unitOfWork)
    : IPipelineHandler<TCommand, TResponse>
    where TCommand : ITransactionalCommand<TResponse>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<TResponse> HandleAsync(TCommand request, PipelineDelegate<TResponse> next, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var response = await next(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return response;
    }

}

