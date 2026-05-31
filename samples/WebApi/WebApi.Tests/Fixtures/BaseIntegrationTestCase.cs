using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.xUnit;

namespace WebApi.Tests.Fixtures;

public abstract class BaseIntegrationTestCase(CustomWebApplicationFactory factory, ITestOutputHelper testOutputHelper) : BaseIntegrationTestCase<CustomWebApplicationFactory, Program>(factory, testOutputHelper), IClassFixture<CustomWebApplicationFactory>
{
    protected ISender Sender => ScopedServices.GetRequiredService<ISender>();
}
