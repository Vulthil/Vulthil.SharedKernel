using Microsoft.EntityFrameworkCore;
using WebApi.Models;

namespace WebApi.Data;

public sealed class WebApiDbContext : DbContext
{
    public DbSet<WebApiEntity> WebApiEntities => Set<WebApiEntity>();
    public WebApiDbContext(DbContextOptions<WebApiDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WebApiDbContext).Assembly);
    }
}
