using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Primitives;

namespace Vulthil.SharedKernel.Infrastructure.Repository;

public abstract class GenericRepository<TDbContext, TEntity, TId>(TDbContext dbContext)
    where TDbContext : DbContext
    where TEntity : Entity<TId>
    where TId : class
{
    protected TDbContext DbContext { get; } = dbContext;

    protected virtual async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<TEntity>().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }
}
