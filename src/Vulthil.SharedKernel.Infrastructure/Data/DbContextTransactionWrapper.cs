using Microsoft.EntityFrameworkCore.Storage;
using Vulthil.SharedKernel.Application.Data;

namespace Vulthil.SharedKernel.Infrastructure.Data;

/// <summary>
/// Wraps an EF Core <see cref="IDbContextTransaction"/> as an <see cref="IDbTransaction"/>.
/// </summary>
/// <param name="dbContextTransaction">The underlying EF Core transaction.</param>
public sealed class DbContextTransactionWrapper(IDbContextTransaction dbContextTransaction) : IDbTransaction
{
    private readonly IDbContextTransaction _dbContextTransaction = dbContextTransaction;

    /// <inheritdoc />
    public Task CommitAsync(CancellationToken cancellationToken = default) => _dbContextTransaction.CommitAsync(cancellationToken);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _dbContextTransaction.DisposeAsync();

    /// <inheritdoc />
    public Task RollbackAsync(CancellationToken cancellationToken = default) => _dbContextTransaction.RollbackAsync(cancellationToken);
}
