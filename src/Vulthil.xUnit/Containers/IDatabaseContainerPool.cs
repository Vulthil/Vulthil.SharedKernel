
using DotNet.Testcontainers.Containers;

namespace Vulthil.xUnit.Containers;

public interface IContainerPool
{
    Task<ICustomContainer> GetContainerAsync();
    void ReleaseContainer(ICustomContainer container);
}

public interface IContainerWithConnectionStringPool : IContainerPool
{
    string KeyName { get; }

    string GetConnectionString(IContainer container);
}

public interface IContainerWithConnectionStringPool<TContainerEntity> : IContainerWithConnectionStringPool
    where TContainerEntity : IContainer
{
    string IContainerWithConnectionStringPool.GetConnectionString(IContainer container) => GetConnectionString((TContainerEntity)container);
    string GetConnectionString(TContainerEntity container);
}

public interface IDatabaseContainerPool : IContainerWithConnectionStringPool
{
    Task ApplyMigrations(IServiceProvider services, ICustomDatabaseContainer container);
}
public interface IDatabaseContainerPool<TContainerEntity> : IDatabaseContainerPool, IContainerWithConnectionStringPool<TContainerEntity>
    where TContainerEntity : IContainer;
