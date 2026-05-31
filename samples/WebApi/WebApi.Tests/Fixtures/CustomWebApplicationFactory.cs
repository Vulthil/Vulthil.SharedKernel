using Vulthil.xUnit;
using WebApi.ExternalServices;
using Xunit.Sdk;

namespace WebApi.Tests.Fixtures;

public sealed class CustomWebApplicationFactory : BaseWebApplicationFactory<Program>
{
    public CustomWebApplicationFactory(IMessageSink messageSink)
    {
        AddContainer(new PostgreSqlTestContainer(messageSink));
        AddContainer(new RabbitMqTestContainer(messageSink));
        AddHttpMock<IExternalWeatherClient>();
        AddHttpMock("inventory");
    }
}
