
using Microsoft.EntityFrameworkCore;
using WebApi.Domain.MainEntities;
using WebApi.Domain.SideEffects;

namespace WebApi.Application;

/// <summary>
/// Represents the IWebApiDbContext.
/// </summary>
public interface IWebApiDbContext
{
    DbSet<MainEntity> MainEntities { get; }
    DbSet<SideEffect> SideEffects { get; }

    /// <summary>
    /// Executes this interface member.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
