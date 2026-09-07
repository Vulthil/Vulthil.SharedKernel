using Vulthil.IntegrationTests.Fixtures;
using Vulthil.xUnit.Fixtures;

[assembly: AssemblyFixture<IntegrationTestContainerHost>]

namespace Vulthil.IntegrationTests.Fixtures;

/// <summary>
/// Assembly-wide container host: one PostgreSQL server, one MySQL server, and one Cosmos emulator for the whole test
/// run. Factories consume them through per-class scopes (a uniquely named database per class), so test classes run
/// in parallel against shared containers without interfering. Containers start lazily, so a filtered run only pays
/// for what its factories consume.
/// </summary>
public sealed class IntegrationTestContainerHost : ContainerHost
{
    protected override Task ConfigureContainers()
    {
        AddContainer<PostgreSqlTestContainer>();
        AddContainer<MySqlTestContainer>();
        AddContainer<CosmosTestContainer>();
        return Task.CompletedTask;
    }
}
