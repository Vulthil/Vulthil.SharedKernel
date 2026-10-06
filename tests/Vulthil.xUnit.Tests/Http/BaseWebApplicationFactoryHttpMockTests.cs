using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Vulthil.xUnit.Fixtures;
using Vulthil.xUnit.Http;

namespace Vulthil.xUnit.Tests.Http;

public sealed class BaseWebApplicationFactoryHttpMockTests : BaseUnitTestCase
{
    [Fact]
    public async Task AGenericTypedClientMockUsesTheNameAddHttpClientGivesTheClient()
    {
        // Arrange
        await using var containerHost = new EmptyContainerHost();
        await using var factory = new MockRegisteringFactory(containerHost);
        var clientName = new ServiceCollection().AddHttpClient<GenericClient<int>>().Name;

        // Act
        var mock = factory.AddMock<GenericClient<int>>();

        // Assert
        factory.GetHttpMock(clientName).ShouldBeSameAs(mock);
        factory.GetHttpMock<GenericClient<int>>().ShouldBeSameAs(mock);
    }

    public sealed class MockRegisteringFactory(ContainerHost containerHost)
        : BaseWebApplicationFactory<MockRegisteringFactory>(containerHost)
    {
        public IHttpMock AddMock<TClient>()
            where TClient : class
            => AddHttpMock<TClient>();
    }

    public sealed class EmptyContainerHost : ContainerHost;

    public sealed class GenericClient<TResponse>(HttpClient client)
    {
        public Task<TResponse?> GetAsync(Uri uri, CancellationToken cancellationToken)
            => client.GetFromJsonAsync<TResponse>(uri, cancellationToken);
    }
}
