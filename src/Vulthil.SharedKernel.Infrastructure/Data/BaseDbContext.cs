using System.Reflection;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Application.Data;

namespace Vulthil.SharedKernel.Infrastructure.Data;

public abstract class BaseDbContext : DbContext, IUnitOfWork
{
    private readonly IPublisher? _publisher;

    protected abstract Assembly? ConfigurationAssembly { get; }
    protected virtual Func<Type, bool>? ConfigurationTypeConstraints { get; }

    protected BaseDbContext(
        DbContextOptions options,
        IPublisher? publisher) : base(options) => _publisher = publisher;

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_publisher is not null)
        {
            return this.SaveAndPublishChangesAsync(_publisher, cancellationToken);
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (ConfigurationAssembly is not null)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(ConfigurationAssembly, ConfigurationTypeConstraints);
        }
    }

    public async Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return new DbContextTransactionWrapper(await Database.BeginTransactionAsync(cancellationToken));
    }
}
