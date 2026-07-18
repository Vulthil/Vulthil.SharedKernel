using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Infrastructure.MySql;

namespace Vulthil.IntegrationTests.Fixtures;

/// <summary>
/// MySQL-mapped context for the provider outbox tests: the MySQL-optimized outbox mapping plus the
/// <see cref="OutboxProbe"/> aggregate whose domain events feed the capture interceptor.
/// </summary>
public sealed class MySqlOutboxDbContext(DbContextOptions<MySqlOutboxDbContext> options) : BaseDbContext(options)
{
    public DbSet<OutboxProbe> Probes => Set<OutboxProbe>();

    protected override Assembly? ConfigurationAssembly => null;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyMySqlOutbox();
        modelBuilder.Entity<OutboxProbe>(entity => entity.HasKey(probe => probe.Id));
    }
}

/// <summary>
/// MySQL-mapped context whose outbox table and columns are renamed to snake_case, proving the relay fetch works
/// against a model that does not use the default identifiers.
/// </summary>
public sealed class RenamedMySqlOutboxDbContext(DbContextOptions<RenamedMySqlOutboxDbContext> options) : BaseDbContext(options)
{
    protected override Assembly? ConfigurationAssembly => null;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyMySqlOutbox();
        OutboxTableRenames.Apply(modelBuilder);
    }
}
