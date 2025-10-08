
using Microsoft.EntityFrameworkCore;
using WebApi.Domain.MainEntities;
using WebApi.Domain.SideEffects;

namespace WebApi.Application;

public interface IWebApiDbContext
{
    DbSet<MainEntity> MainEntities { get; }
    DbSet<SideEffect> SideEffects { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
