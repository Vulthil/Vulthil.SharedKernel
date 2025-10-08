

namespace Vulthil.xUnit.Fixtures;

public interface ITestContainer : IAsyncLifetime;
public interface ITestContainerWithConnectionString : ITestContainer
{
    string ConnectionString { get; }
    string ConnectionStringKey { get; }
}
public interface ITestDatabaseContainer : ITestContainerWithConnectionString
{
    ValueTask InitializeRespawner();
    ValueTask MigrateDatabase(IServiceProvider serviceProvider);
    ValueTask ResetDatabase();
}
