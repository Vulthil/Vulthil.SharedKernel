
using Microsoft.EntityFrameworkCore;

namespace WebApi.Application;
public interface IWebApiDbContext
{
    DbSet<Domain.WebApiEntityModel.WebApiEntity> WebApiEntities { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
