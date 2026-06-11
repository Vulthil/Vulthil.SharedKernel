using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage;
using Vulthil.Messaging.Inbox.EntityFrameworkCore;
using Vulthil.Messaging.Inbox.Relational;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Outbox;
using Vulthil.TestHost.Probes;

namespace Vulthil.TestHost.Data;

/// <summary>
/// The host's database context: probe side effects plus the outbox (via <see cref="BaseDbContext"/>) and the
/// relational inbox, so the integration tests can observe both pipelines against a real PostgreSQL database.
/// </summary>
public sealed class TestHostDbContext(DbContextOptions<TestHostDbContext> options) : BaseDbContext(options), ISaveInboxMessages
{
    public DbSet<ProbeSideEffect> ProbeSideEffects => Set<ProbeSideEffect>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    protected override Assembly? ConfigurationAssembly => typeof(TestHostDbContext).Assembly;
}

internal sealed class ProbeSideEffectConfiguration : IEntityTypeConfiguration<ProbeSideEffect>
{
    public void Configure(EntityTypeBuilder<ProbeSideEffect> builder)
    {
        builder.HasKey(sideEffect => sideEffect.Id);
        builder.HasIndex(sideEffect => sideEffect.ProbeId);
    }
}

internal sealed class TestHostOutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder) =>
        new SharedKernel.Infrastructure.Npgsql.OutboxProcessing.OutboxMessageEntityConfiguration().Configure(builder);
}

internal sealed class TestHostInboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder) =>
        new InboxMessageEntityConfiguration().Configure(builder);
}

public sealed class TestHostDbContextFactory : IDesignTimeDbContextFactory<TestHostDbContext>
{
    public TestHostDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TestHostDbContext>();

#pragma warning disable S2068 // Credentials should not be hard-coded
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=__dummy__;Username=__dummy__;Password=__dummy__;Pooling=false",
            o => o.ExecutionStrategy(d => new NonRetryingExecutionStrategy(d)));
#pragma warning restore S2068 // Credentials should not be hard-coded

        return new TestHostDbContext(optionsBuilder.Options);
    }
}
