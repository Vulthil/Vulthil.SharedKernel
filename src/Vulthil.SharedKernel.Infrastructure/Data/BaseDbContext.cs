using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Application.Data;
using Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

namespace Vulthil.SharedKernel.Infrastructure.Data;

/// <summary>
/// Abstract base <see cref="DbContext"/> that implements <see cref="IUnitOfWork"/> and <see cref="ISaveOutboxMessages"/> for outbox integration.
/// </summary>
/// <param name="options">The options for configuring the context.</param>
public abstract class BaseDbContext(DbContextOptions options) : DbContext(options), IUnitOfWork, ISaveOutboxMessages
{
    /// <inheritdoc />
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// Gets the assembly containing EF Core entity type configurations to apply during model creation.
    /// Return <see langword="null"/> to skip automatic configuration scanning.
    /// </summary>
    protected abstract Assembly? ConfigurationAssembly { get; }
    /// <summary>
    /// Gets an optional type filter predicate applied when scanning for entity type configurations.
    /// When <see langword="null"/>, all configurations in <see cref="ConfigurationAssembly"/> are applied.
    /// </summary>
    protected virtual Func<Type, bool>? ConfigurationTypeConstraints { get; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new OutboxMessageEntityConfiguration());

        if (ConfigurationAssembly is not null)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(ConfigurationAssembly, ConfigurationTypeConstraints);
        }
    }

    /// <inheritdoc />
    public async Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => new DbContextTransactionWrapper(await Database.BeginTransactionAsync(cancellationToken));
}
