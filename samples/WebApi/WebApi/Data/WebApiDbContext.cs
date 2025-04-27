using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vulthil.SharedKernel.Application.Data;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Infrastructure.OutboxProcessing;
using WebApi.Models;

namespace WebApi.Data;

/// <summary>
/// Functionally equivalent with <see cref="WebApiDbContextNoBase"/>, by inheriting from <see cref="BaseDbContext"/>.
/// </summary>
/// <param name="options"></param>
public sealed class WebApiDbContext(DbContextOptions<WebApiDbContext> options) : BaseDbContext(options)
{
    public DbSet<WebApiEntity> WebApiEntities => Set<WebApiEntity>();
    protected override Assembly? ConfigurationAssembly => typeof(WebApiDbContext).Assembly;

    protected override Func<Type, bool>? ConfigurationTypeConstraints => base.ConfigurationTypeConstraints;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>
/// Functionally equivalent with <see cref="WebApiDbContext"/>, without inheriting from <see cref="BaseDbContext"/>.
/// </summary>
/// <param name="options"></param>
public sealed class WebApiDbContextNoBase(DbContextOptions<WebApiDbContextNoBase> options) : DbContext(options), IUnitOfWork, ISaveOutboxMessages
{
    public DbSet<WebApiEntity> WebApiEntities => Set<WebApiEntity>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WebApiDbContextNoBase).Assembly);
    }

    public async Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => new DbContextTransactionWrapper(await Database.BeginTransactionAsync(cancellationToken));
}

internal sealed class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(o => o.Id);
        builder.HasIndex(o => new { o.OccurredOnUtc, o.ProcessedOnUtc })
            .HasFilter($"\"{nameof(OutboxMessage.ProcessedOnUtc)}\" IS NULL")
            .IncludeProperties(o => new { o.Id, o.Type, o.Content });
    }
}
