using System.Data.Common;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Respawn;

namespace Vulthil.SharedKernel.xUnit.Containers;

public abstract class ContainerWithConnectionStringPool<TContainerType, TBuilderEntity, TContainerEntity> : ContainerPool<TContainerType, TBuilderEntity, TContainerEntity>, IContainerWithConnectionStringPool<TContainerEntity>
    where TContainerType : ICustomContainer
    where TContainerEntity : IContainer
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity>
{
    public abstract string KeyName { get; }
    public abstract string GetConnectionString(TContainerEntity container);
}

public abstract class ContainerWithConnectionStringPool<TBuilderEntity, TContainerEntity> : ContainerWithConnectionStringPool<ICustomContainer, TBuilderEntity, TContainerEntity>
where TContainerEntity : IContainer
where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity>
{
    protected override ICustomContainer CreateCustomContainer(TContainerEntity container) => new ContainerWrapper(container);
}

public abstract class DatabaseContainerPool<TDbContext, TBuilderEntity, TContainerEntity> : ContainerWithConnectionStringPool<ICustomDatabaseContainer, TBuilderEntity, TContainerEntity>, IDatabaseContainerPool
    where TDbContext : DbContext
    where TContainerEntity : IDatabaseContainer
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity>
{
    public override string GetConnectionString(TContainerEntity container) => container.GetConnectionString();
    public virtual Task ApplyMigrations(IServiceProvider services, ICustomDatabaseContainer container) => Task.CompletedTask;
    protected override ICustomDatabaseContainer CreateCustomContainer(TContainerEntity container) =>
        new DatabaseContainer<TDbContext>(container);
}
public abstract class DatabaseContainerWithRespawnerPool<TDbContext, TBuilderEntity, TContainerEntity> : DatabaseContainerPool<TDbContext, TBuilderEntity, TContainerEntity>
    where TDbContext : DbContext
    where TContainerEntity : IDatabaseContainer
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity>
{
    protected abstract Task<DbConnection> GetOpenConnectionAsync(string connectionString);
    protected abstract RespawnerOptions RespawnerOptions { get; }
    protected override ICustomDatabaseContainerWithRespawner CreateCustomContainer(TContainerEntity container) =>
        new DatabaseContainerWithRespawner<TDbContext>(container, RespawnerOptions, GetOpenConnectionAsync);

    public override async Task ApplyMigrations(IServiceProvider services, ICustomDatabaseContainer container)
    {
        if (container.HasBeenMigrated)
        {
            return;
        }

        await using var dbContext = services.GetRequiredService<TDbContext>();
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            await dbContext.Database.MigrateAsync();
        }

        container.MarkMigrated();
    }
}
