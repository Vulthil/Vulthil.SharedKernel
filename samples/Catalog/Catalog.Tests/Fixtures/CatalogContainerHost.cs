using Catalog.Tests.Fixtures;
using Vulthil.xUnit.Fixtures;

[assembly: AssemblyFixture<CatalogContainerHost>]

namespace Catalog.Tests.Fixtures;

/// <summary>
/// One Cosmos DB emulator for the whole test run; each factory gets an emulator database of its own. There is no
/// broker container: the factory swaps RabbitMQ for the in-memory test harness.
/// </summary>
public sealed class CatalogContainerHost : ContainerHost
{
    protected override Task ConfigureContainers()
    {
        AddContainer<CatalogCosmosContainer>();
        return Task.CompletedTask;
    }
}
