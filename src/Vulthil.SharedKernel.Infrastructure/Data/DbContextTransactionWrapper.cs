using Microsoft.EntityFrameworkCore.Storage;
using Vulthil.SharedKernel.Application.Data;

namespace Vulthil.SharedKernel.Infrastructure.Data;

internal class DbContextTransactionWrapper : IDbTransaction
{
    private readonly IDbContextTransaction _dbContextTransaction;

    public DbContextTransactionWrapper(IDbContextTransaction dbContextTransaction) => _dbContextTransaction = dbContextTransaction;

    public Task CommitAsync(CancellationToken cancellationToken = default) => _dbContextTransaction.CommitAsync(cancellationToken);

    public ValueTask DisposeAsync() => _dbContextTransaction.DisposeAsync();

    public Task RollbackAsync(CancellationToken cancellationToken = default) => _dbContextTransaction.RollbackAsync(cancellationToken);
}
