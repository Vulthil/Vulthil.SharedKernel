using DotNet.Testcontainers.Containers;

namespace Vulthil.xUnit.Containers;

public interface ICustomContainer : IAsyncDisposable
{
    IContainer Container { get; }
}

public interface ICustomDatabaseContainer : ICustomContainer
{
    Type DbContextType { get; }
    bool HasBeenMigrated { get; }

    void MarkMigrated();
}

public interface ICustomDatabaseContainerWithRespawner : ICustomDatabaseContainer
{
    Task InitializeRespawner();
    Task ResetAsync();
}
