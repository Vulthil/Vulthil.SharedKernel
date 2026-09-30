using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Infrastructure.Npgsql;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

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
/// PostgreSQL-mapped context whose outbox table and columns are renamed to snake_case, proving the relay fetch works
/// against a model that does not use the default identifiers. It uses the provider-agnostic outbox mapping because
/// the Npgsql-optimized index filter is expressed against the default column names.
/// </summary>
public sealed class RenamedNpgsqlOutboxDbContext(DbContextOptions<RenamedNpgsqlOutboxDbContext> options) : BaseDbContext(options)
{
    protected override Assembly? ConfigurationAssembly => null;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyOutbox();
        OutboxTableRenames.Apply(modelBuilder);
    }
}
