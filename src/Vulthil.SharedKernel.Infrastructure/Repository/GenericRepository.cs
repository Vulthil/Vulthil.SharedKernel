using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Primitives;

namespace Vulthil.SharedKernel.Infrastructure.Repository;

/// <summary>
/// Base repository providing common data access operations for aggregate roots.
/// </summary>
/// <typeparam name="TDbContext">The type of EF Core DbContext.</typeparam>
/// <typeparam name="TEntity">The aggregate root entity type.</typeparam>
/// <typeparam name="TId">The identifier type of the aggregate root.</typeparam>
/// <param name="dbContext">The database context instance.</param>
public abstract class GenericRepository<TDbContext, TEntity, TId>(TDbContext dbContext)
    where TDbContext : DbContext
    where TEntity : AggregateRoot<TId>
    where TId : class
{
    /// <summary>
    /// Gets the database context used for data access operations. Derived repositories use this
    /// to query and persist aggregate roots.
    /// </summary>
    protected TDbContext DbContext { get; } = dbContext;

    /// <summary>
    /// Retrieves an entity by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the entity to retrieve.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The entity if found; otherwise, <see langword="null"/>.</returns>
    protected virtual async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default) =>
        await DbContext.Set<TEntity>().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
}
