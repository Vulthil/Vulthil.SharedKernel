using Microsoft.EntityFrameworkCore;

namespace WebApi.Tests.Fixtures;

/// <summary>
/// A minimal Cosmos-mapped context used to verify the <see cref="CosmosTestContainer{TDbContext}"/> end to end.
/// </summary>
internal sealed class CosmosProbeDbContext(DbContextOptions<CosmosProbeDbContext> options) : DbContext(options)
{
    public DbSet<CosmosProbe> Probes => Set<CosmosProbe>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<CosmosProbe>(entity =>
        {
            entity.ToContainer("Probes");
            entity.HasNoDiscriminator();
            entity.HasKey(probe => probe.Id);
            entity.HasPartitionKey(probe => probe.Id);
        });
    }
}

internal sealed class CosmosProbe
{
    public required string Id { get; set; }

    public required string Value { get; set; }
}
