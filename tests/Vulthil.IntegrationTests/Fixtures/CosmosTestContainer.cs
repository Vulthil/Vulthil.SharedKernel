using Testcontainers.CosmosDb;
using Vulthil.TestHost;
using Vulthil.xUnit.Cosmos;

namespace Vulthil.IntegrationTests.Fixtures;

internal sealed class CosmosTestContainer : CosmosTestContainerFixture<CosmosProbeDbContext>
{
    private const string CosmosDbImage = "mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-latest";

    protected override CosmosDbBuilder Configure() => new(CosmosDbImage);

    public override string ConnectionStringKey => TestHostConnectionStrings.CosmosDb;
}
