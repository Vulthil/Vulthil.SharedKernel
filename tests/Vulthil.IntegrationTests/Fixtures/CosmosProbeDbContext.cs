using Microsoft.EntityFrameworkCore;
using Vulthil.Messaging.Inbox.Cosmos;
using Vulthil.Messaging.Inbox.EntityFrameworkCore;
using Vulthil.SharedKernel.Infrastructure.Cosmos;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

namespace Vulthil.IntegrationTests.Fixtures;

/// <summary>
/// A minimal Cosmos-mapped context used to verify the Cosmos test container fixture end to end. It also implements
/// <see cref="ISaveInboxMessages"/> and maps a side-effect set so the same emulator backs the Cosmos
/// idempotency-store integration test, and <see cref="ISaveOutboxMessages"/> with the stock Cosmos outbox mapping so
/// the Cosmos outbox store is exercised against a container created with default indexing.
/// </summary>
internal sealed class CosmosProbeDbContext(DbContextOptions<CosmosProbeDbContext> options) : DbContext(options), ISaveInboxMessages, ISaveOutboxMessages
{
    public DbSet<CosmosProbe> Probes => Set<CosmosProbe>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<CosmosSideEffect> SideEffects => Set<CosmosSideEffect>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public bool IsInTransaction => Database.CurrentTransaction is not null;

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

        modelBuilder.ApplyCosmosInbox();
        modelBuilder.ApplyCosmosOutbox();

        modelBuilder.Entity<CosmosSideEffect>(entity =>
        {
            entity.ToContainer("SideEffects");
            entity.HasNoDiscriminator();
            entity.HasKey(sideEffect => sideEffect.Id);
            entity.HasPartitionKey(sideEffect => sideEffect.Id);
        });
    }
}

internal sealed class CosmosProbe
{
    public required string Id { get; set; }

    public required string Value { get; set; }
}

internal sealed class CosmosSideEffect
{
    public required string Id { get; set; }

    public required string Key { get; set; }
}
