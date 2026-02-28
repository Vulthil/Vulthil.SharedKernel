using Microsoft.Extensions.DependencyInjection;
using Vulthil.SharedKernel.Application.Messaging;
using Vulthil.xUnit;

namespace WebApi.Tests.Fixtures;

/// <summary>
/// Represents the BaseIntegrationTestCase.
/// </summary>
public abstract class BaseIntegrationTestCase(FixtureWrapper testFixture, ITestOutputHelper testOutputHelper) : BaseIntegrationTestCase<CustomWebApplicationFactory, Program>(testFixture, testOutputHelper), IClassFixture<FixtureWrapper>
{
    /// <summary>
    /// Executes this member.
    /// </summary>
    protected ISender Sender => ScopedServices.GetRequiredService<ISender>();
}
