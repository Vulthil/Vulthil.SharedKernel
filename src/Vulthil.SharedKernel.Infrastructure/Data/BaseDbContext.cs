using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Application.Data;
using Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

namespace Vulthil.SharedKernel.Infrastructure.Data;

public abstract class BaseDbContext(DbContextOptions options) : DbContext(options), IUnitOfWork, ISaveOutboxMessages
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected abstract Assembly? ConfigurationAssembly { get; }
    protected virtual Func<Type, bool>? ConfigurationTypeConstraints { get; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (ConfigurationAssembly is not null)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(ConfigurationAssembly, ConfigurationTypeConstraints);
        }
    }

    public async Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => new DbContextTransactionWrapper(await Database.BeginTransactionAsync(cancellationToken));
}
