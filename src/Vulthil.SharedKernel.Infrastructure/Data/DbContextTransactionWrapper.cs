using Microsoft.EntityFrameworkCore.Storage;
using Vulthil.SharedKernel.Application.Data;

namespace Vulthil.SharedKernel.Infrastructure.Data;

public sealed class DbContextTransactionWrapper(IDbContextTransaction dbContextTransaction) : IDbTransaction
{
    private readonly IDbContextTransaction _dbContextTransaction = dbContextTransaction;

    public Task CommitAsync(CancellationToken cancellationToken = default) => _dbContextTransaction.CommitAsync(cancellationToken);

    public ValueTask DisposeAsync() => _dbContextTransaction.DisposeAsync();

    public Task RollbackAsync(CancellationToken cancellationToken = default) => _dbContextTransaction.RollbackAsync(cancellationToken);
}
