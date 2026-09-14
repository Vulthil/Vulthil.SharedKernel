using Microsoft.Extensions.DependencyInjection;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.Messaging.Abstractions.Publishers;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Covers the test-case service overrides of <see cref="BaseIntegrationTestCase{TEntryPoint}"/>: a service registered
/// on the test case before the host is touched replaces the application's own registration on a per-test host, while
/// a test that registers nothing keeps running on the shared fixture host.
/// </summary>
public sealed class TestCaseServiceOverrideTests(TestHarnessWebApplicationFactory factory)
    : BaseIntegrationTestCase<Program>(factory), IClassFixture<TestHarnessWebApplicationFactory>
{
    [Fact]
    public void AMockRegisteredBeforeTheHostIsBuiltReplacesTheApplicationsServiceOnAPerTestHost()
    {
        // Arrange
        var publisher = GetMock<IPublisher>();

        // Act
        var resolved = ScopedServices.GetRequiredService<IPublisher>();

        // Assert
        resolved.ShouldBeSameAs(publisher.Object);
        Factory.ShouldNotBeSameAs(FactoryFixture);
    }

    [Fact]
    public void ATestWithoutRegistrationsRunsOnTheSharedFixtureHost()
    {
        // Act & Assert
        Factory.ShouldBeSameAs(FactoryFixture);
        ScopedServices.GetRequiredService<IPublisher>().ShouldNotBeOfType<Mock<IPublisher>>();
    }

    [Fact]
    public void RegisteringAServiceAfterTheHostIsBuiltFails()
    {
        // Arrange
        _ = Factory.Services;

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => GetMock<IPublisher>());
        exception.Message.ShouldContain(nameof(IPublisher));
    }
}
