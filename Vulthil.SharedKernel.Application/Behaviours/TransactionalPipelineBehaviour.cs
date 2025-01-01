using MediatR;
using Vulthil.SharedKernel.Application.Data;
using Vulthil.SharedKernel.Application.Messaging;

namespace Vulthil.SharedKernel.Application.Behaviours;

public sealed class TransactionalPipelineBehaviour<TRequest, TResponse>(
    IUnitOfWork unitOfWork)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ITransactionalCommand
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var response = await next();

        await transaction.CommitAsync(cancellationToken);

        return response;
    }
}
