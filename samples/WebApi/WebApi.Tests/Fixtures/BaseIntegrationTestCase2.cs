using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.xUnit;

namespace WebApi.Tests.Fixtures;

public abstract class BaseIntegrationTestCase2(FixtureWrapper testFixture, ITestOutputHelper testOutputHelper) : BaseIntegrationTestCase2<CustomWebApplicationFactory2, Program>(testFixture, testOutputHelper), IClassFixture<FixtureWrapper>
{
    protected ISender Sender => ScopedServices.GetRequiredService<ISender>();
}
