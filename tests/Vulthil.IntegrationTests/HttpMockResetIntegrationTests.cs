using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.Messaging.TestHarness;
using Vulthil.TestHost;
using Vulthil.xUnit;
using Vulthil.xUnit.Fixtures;

namespace Vulthil.IntegrationTests;

/// <summary>
/// Covers the HTTP mock reset in <see cref="BaseIntegrationTestCase{TEntryPoint}"/>: the teardown after a test clears
/// the configured responses and the captured requests of every HTTP mock the factory registered, so the next test of
/// the class starts clean.
/// </summary>
public sealed class HttpMockResetIntegrationTests(HttpMockResetIntegrationTests.HttpMockWebApplicationFactory factory, ITestOutputHelper testOutputHelper)
    : BaseIntegrationTestCase<Program>(factory, testOutputHelper), IClassFixture<HttpMockResetIntegrationTests.HttpMockWebApplicationFactory>
{
    private static readonly Uri ProbeUri = new("http://probe.test/probe");

    private bool _disposedOnce;

    [Fact]
    public async Task TheTeardownAfterATestClearsTheHttpMockResponsesAndCapturedRequests()
    {
        // Arrange
        var mock = HttpMock(HttpMockWebApplicationFactory.ClientName);
        mock.On(HttpMethod.Get, "/probe").RespondWith(HttpStatusCode.Accepted);
        using var beforeReset = await SendProbeAsync();

        // Act
        await DisposeAsync();
        using var afterReset = await SendProbeAsync();

        // Assert
        beforeReset.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        afterReset.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
        mock.ReceivedRequests.ShouldHaveSingleItem();
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        if (_disposedOnce)
        {
            return;
        }

        _disposedOnce = true;
        await base.DisposeAsyncCore();
    }

    private async Task<HttpResponseMessage> SendProbeAsync()
    {
        using var client = Factory.Services.GetRequiredService<IHttpClientFactory>().CreateClient(HttpMockWebApplicationFactory.ClientName);
        return await client.GetAsync(ProbeUri, CancellationToken);
    }

    /// <summary>
    /// Boots the test host against the shared PostgreSQL server only, with the broker transport swapped for the
    /// in-memory harness, and routes the named <see cref="ClientName"/> client through an HTTP mock.
    /// </summary>
    public sealed class HttpMockWebApplicationFactory : BaseWebApplicationFactory<Program>
    {
        public const string ClientName = "probe";

        public HttpMockWebApplicationFactory(IntegrationTestContainerHost containerHost) : base(containerHost)
            => AddHttpMock(ClientName);

        protected override bool ShouldUseContainer(ITestContainer container) => container is PostgreSqlTestContainer;

        protected override void ConfigureCustomWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // No broker container is consumed, so stub the connection string to satisfy the RabbitMQ client
            // registration; swapping the transport means the broker connection is never resolved.
            builder.UseSetting($"ConnectionStrings:{TestHostConnectionStrings.RabbitMq}", "amqp://guest:guest@localhost:5672");
            builder.ConfigureServices(services =>
            {
                services.ReplaceTransportWithTestHarness();
                services.AddHttpClient(ClientName);
            });
        }
    }
}
