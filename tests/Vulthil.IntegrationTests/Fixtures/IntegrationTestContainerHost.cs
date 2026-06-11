using Vulthil.IntegrationTests.Fixtures;
using Vulthil.xUnit.Fixtures;
using Xunit.Sdk;

[assembly: AssemblyFixture(typeof(IntegrationTestContainerHost))]

namespace Vulthil.IntegrationTests.Fixtures;

/// <summary>
/// Assembly-wide container host: one PostgreSQL server and one Cosmos emulator for the whole test run. Factories
/// consume them through per-class scopes (a uniquely named database per class), so test classes run in parallel
/// against shared containers without interfering. Containers start lazily, so a filtered run only pays for what its
/// factories consume.
/// </summary>
public sealed class IntegrationTestContainerHost(IMessageSink messageSink) : ContainerHost(messageSink)
{
    protected override Task ConfigureContainers()
    {
#pragma warning disable CA2000 // Ownership transfers to the host; containers are disposed at assembly end.
        AddContainer(new PostgreSqlTestContainer(MessageSink));
        AddContainer(new CosmosTestContainer(MessageSink));
#pragma warning restore CA2000
        return Task.CompletedTask;
    }
}
