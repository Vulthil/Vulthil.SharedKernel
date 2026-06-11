using Vulthil.xUnit;
using WebApi.ExternalServices;

namespace WebApi.Tests.Fixtures;

/// <summary>
/// Boots the WebApi sample against the shared <see cref="AppContainerHost"/> containers: each test class gets its
/// own PostgreSQL database and RabbitMQ virtual host, so classes using this factory run in parallel without
/// interfering.
/// </summary>
public sealed class CustomWebApplicationFactory : BaseWebApplicationFactory<Program>
{
    public CustomWebApplicationFactory(AppContainerHost containerHost) : base(containerHost)
    {
        AddHttpMock<IExternalWeatherClient>();
        AddHttpMock("inventory");
    }
}
