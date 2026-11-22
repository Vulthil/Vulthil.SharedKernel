using Shipments.Tests.Fixtures;
using Vulthil.xUnit.Fixtures;

[assembly: AssemblyFixture<ShipmentsContainerHost>]

namespace Shipments.Tests.Fixtures;

/// <summary>
/// One MySQL server and one RabbitMQ broker for the whole test run. Each factory gets its own database and virtual
/// host on them; a factory that swaps the broker for the test harness consumes only the database.
/// </summary>
public sealed class ShipmentsContainerHost : ContainerHost
{
    protected override Task ConfigureContainers()
    {
        AddContainer<MySqlTestContainer>();
        AddContainer<RabbitMqTestContainer>();
        return Task.CompletedTask;
    }
}
