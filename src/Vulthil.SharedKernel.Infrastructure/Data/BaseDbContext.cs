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

        // If the DbContext's ConfigurationAssembly contains a provider-specific IEntityTypeConfiguration<OutboxMessage>,
        // prefer that configuration. Otherwise fall back to the built-in provider-agnostic configuration.
        var outboxHasProviderConfig = false;

        if (ConfigurationAssembly is not null)
        {
            outboxHasProviderConfig = ConfigurationAssembly
                .GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .SelectMany(t => t.GetInterfaces())
                .Any(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>) &&
                    i.GenericTypeArguments.Length == 1 &&
                    i.GenericTypeArguments[0] == typeof(OutboxMessage));
        }

        if (!outboxHasProviderConfig)
        {
            modelBuilder.ApplyConfiguration(new OutboxMessageEntityConfiguration());
        }

        if (ConfigurationAssembly is not null)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(ConfigurationAssembly, ConfigurationTypeConstraints);
        }
    }

    /// <inheritdoc />
    public async Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => new DbContextTransactionWrapper(await Database.BeginTransactionAsync(cancellationToken));

    /// <inheritdoc />
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var strategy = Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            async token =>
            {
                ChangeTracker.Clear();
                await using var transaction = await Database.BeginTransactionAsync(token);
                var result = await operation(token);
                await transaction.CommitAsync(token);
                return result;
            },
            cancellationToken);
    }
}
