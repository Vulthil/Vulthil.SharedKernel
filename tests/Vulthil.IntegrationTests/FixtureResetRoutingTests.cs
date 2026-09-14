using Microsoft.AspNetCore.Mvc.Testing;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.xUnit;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Covers the reset routing in <see cref="BaseIntegrationTestCase{TEntryPoint}"/>: overriding <c>CreateFactory()</c>
/// to run a test on a <c>WithWebHostBuilder(...)</c>-derived factory must reset that derived host, never the class
/// fixture's own (separate, otherwise-unused) host.
/// </summary>
public sealed class FixtureResetRoutingTests(RestartProbeWebApplicationFactory factory)
    : BaseIntegrationTestCase<Program>(factory), IClassFixture<RestartProbeWebApplicationFactory>
{
    private bool _disposedOnce;

    private RestartProbeWebApplicationFactory ProbeFactory => (RestartProbeWebApplicationFactory)FactoryFixture;

    protected override WebApplicationFactory<Program> CreateFactory() => FactoryFixture.WithWebHostBuilder(_ => { });

    [Fact]
    public async Task ResettingAfterATestPausesAndResumesOnlyTheHostTheTestActuallyUsed()
    {
        // Arrange — forces the derived (WithWebHostBuilder) host to build and start; ASP.NET Core auto-starts its
        // registered IHostedServices, including this factory's RestartableProbe.
        _ = Factory.Services;

        // Act — runs the same reset dance xUnit would run automatically at the end of this test.
        await DisposeAsync();

        // Assert — the reset dance ran exactly once, on the one host this test built: an initial auto-start, then
        // the reset's own stop/start pair. Trailing "stop" events beyond that come from the derived factory's own
        // teardown disposing its host afterwards (unrelated to the reset) and are intentionally not asserted here.
        // Exactly two "start" events total is the key signal: the pre-fix code additionally built (and
        // auto-started) FactoryFixture's own, otherwise-unused host as a side effect of resetting through it
        // directly instead of the host this test actually ran on.
        var events = ProbeFactory.Events.ToArray();
        events.Take(3).ShouldBe(["start", "stop", "start"]);
        events.Count(e => e == "start").ShouldBe(2);
    }

    /// <inheritdoc />
    protected override async ValueTask Dispose()
    {
        if (_disposedOnce)
        {
            return;
        }

        _disposedOnce = true;
        await base.Dispose();
    }
}
