using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage;
using Vulthil.SharedKernel.Application.Data;
using Vulthil.SharedKernel.Infrastructure.Data;
using Vulthil.SharedKernel.Infrastructure.OutboxProcessing;
using WebApi.Application;
using WebApi.Domain.MainEntities;
using WebApi.Domain.SideEffects;

namespace WebApi.Infrastructure.Data;

/// <summary>
/// Functionally equivalent with <see cref="WebApiDbContextNoBase"/>, by inheriting from <see cref="BaseDbContext"/>.
/// </summary>
/// <param name="options"></param>
public sealed class WebApiDbContext(DbContextOptions<WebApiDbContext> options) : BaseDbContext(options), IWebApiDbContext
{
    /// <summary>
    /// Executes this member.
    /// </summary>
    public DbSet<MainEntity> MainEntities => Set<MainEntity>();
    /// <summary>
    /// Executes this member.
    /// </summary>
    public DbSet<SideEffect> SideEffects => Set<SideEffect>();
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected override Assembly? ConfigurationAssembly => typeof(WebApiDbContext).Assembly;
}

/// <summary>
/// Functionally equivalent with <see cref="WebApiDbContext"/>, without inheriting from <see cref="BaseDbContext"/>.
/// </summary>
/// <param name="options"></param>
public sealed class WebApiDbContextNoBase(DbContextOptions<WebApiDbContextNoBase> options) : DbContext(options), IUnitOfWork, ISaveOutboxMessages, IWebApiDbContext
{
    /// <summary>
    /// Executes this member.
    /// </summary>
    public DbSet<MainEntity> MainEntities => Set<MainEntity>();
    /// <summary>
    /// Executes this member.
    /// </summary>
    public DbSet<SideEffect> SideEffects => Set<SideEffect>();
    /// <summary>
    /// Executes this member.
    /// </summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// Executes this member.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WebApiDbContextNoBase).Assembly);
    }

    /// <summary>
    /// Executes this member.
    /// </summary>
    public async Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => new DbContextTransactionWrapper(await Database.BeginTransactionAsync(cancellationToken));
}

internal sealed class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    /// <summary>
    /// Executes this member.
    /// </summary>
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(o => o.Id);
        builder.HasIndex(o => new { o.OccurredOnUtc, o.ProcessedOnUtc })
            .HasFilter($"\"{nameof(OutboxMessage.ProcessedOnUtc)}\" IS NULL")
            .IncludeProperties(o => new { o.Id, o.Type, o.Content });

        builder.Property(o => o.Content)
            .HasColumnType("jsonb");
    }
}

/// <summary>
/// Represents the WebApiDbContextFactory.
/// </summary>
public class WebApiDbContextFactory : IDesignTimeDbContextFactory<WebApiDbContext>
{
    /// <summary>
    /// Executes this member.
    /// </summary>
    public WebApiDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WebApiDbContext>();

#pragma warning disable S2068 // Credentials should not be hard-coded
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=__dummy__;Username=__dummy__;Password=__dummy__;Pooling=false",
 o => o.ExecutionStrategy(d => new NonRetryingExecutionStrategy(d)));
#pragma warning restore S2068 // Credentials should not be hard-coded

        return new WebApiDbContext(optionsBuilder.Options);
    }
}
