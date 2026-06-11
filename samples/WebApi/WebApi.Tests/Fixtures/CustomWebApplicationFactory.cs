using Vulthil.xUnit;
using Vulthil.xUnit.Fixtures;
using WebApi.ExternalServices;

namespace WebApi.Tests.Fixtures;

/// <summary>
/// Boots the WebApi sample against the shared <see cref="AppContainerHost"/> containers: it receives its own
/// PostgreSQL database and RabbitMQ virtual host per test class, so classes using this factory run in parallel
/// without interfering. The Cosmos emulator is excluded — it belongs to <see cref="CosmosWebApplicationFactory"/>'s
/// scenario.
/// </summary>
public sealed class CustomWebApplicationFactory : BaseWebApplicationFactory<Program>
{
    public CustomWebApplicationFactory(AppContainerHost containerHost) : base(containerHost)
    {
        AddHttpMock<IExternalWeatherClient>();
        AddHttpMock("inventory");
    }

    protected override bool ShouldUseContainer(ITestContainer container) =>
        container is not CosmosTestContainer;
}
