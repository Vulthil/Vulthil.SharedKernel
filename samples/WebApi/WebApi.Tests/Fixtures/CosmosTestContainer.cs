using ServiceDefaults;
using Testcontainers.CosmosDb;
using Vulthil.xUnit.Cosmos;
using Xunit.Sdk;

namespace WebApi.Tests.Fixtures;

internal sealed class CosmosTestContainer(IMessageSink messageSink) : CosmosTestContainerFixture<CosmosProbeDbContext>(messageSink)
{
    private const string CosmosDbImage = "mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-latest";

    protected override CosmosDbBuilder Configure() => new(CosmosDbImage);

    public override string ConnectionStringKey => ServiceNames.CosmosDbServiceName;
}
