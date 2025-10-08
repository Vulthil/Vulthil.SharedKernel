using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.xUnit;

namespace WebApi.Tests.Fixtures;

public abstract class BaseIntegrationTestCase(FixtureWrapper testFixture, ITestOutputHelper testOutputHelper) : BaseIntegrationTestCase<CustomWebApplicationFactory, Program>(testFixture, testOutputHelper), IClassFixture<FixtureWrapper>
{
    protected ISender Sender => ScopedServices.GetRequiredService<ISender>();
}
