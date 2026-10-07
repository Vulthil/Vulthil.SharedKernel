using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Infrastructure.Npgsql;

namespace Vulthil.IntegrationTests.Fixtures;

/// <summary>
/// PostgreSQL-mapped context for the relational outbox store tests: the Npgsql-optimized outbox mapping plus the
/// <see cref="OutboxProbe"/> aggregate whose domain events feed the capture interceptor.
/// </summary>
public sealed class NpgsqlOutboxDbContext(DbContextOptions<NpgsqlOutboxDbContext> options) : BaseDbContext(options)
{
    public DbSet<OutboxProbe> Probes => Set<OutboxProbe>();

    protected override Assembly? ConfigurationAssembly => null;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyNpgsqlOutbox();
        modelBuilder.Entity<OutboxProbe>(entity => entity.HasKey(probe => probe.Id));
    }
}

/// <summary>
/// PostgreSQL-mapped context whose outbox table and columns are renamed to snake_case after the Npgsql-optimized
/// outbox mapping, proving the relay fetch and the pending-message index filter follow a model that does not use the
/// default identifiers.
/// </summary>
public sealed class RenamedNpgsqlOutboxDbContext(DbContextOptions<RenamedNpgsqlOutboxDbContext> options) : BaseDbContext(options)
{
    protected override Assembly? ConfigurationAssembly => null;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyNpgsqlOutbox();
        OutboxTableRenames.Apply(modelBuilder);
    }
}
