using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.CosmosDb;
using Vulthil.xUnit.Containers;
using WebApi.Data;
using WebApi.ServiceDefaults;

namespace WebApi.Tests;

public sealed class TempCosmos : DatabaseContainerPool<CosmosDbContext, CosmosDbBuilder, CosmosDbContainer>
{
    public override string KeyName => ServiceNames.CosmosServiceName;

    private readonly string _dbName = $"cosmos-db-{Guid.NewGuid()}";
    protected override int PoolSize => 1;

    protected override IContainerBuilder<CosmosDbBuilder, CosmosDbContainer> ContainerBuilder => new CosmosDbBuilder();

    public override void ConfigureCustomServices(IServiceCollection services, CosmosDbContainer container)
    {
        services.RemoveAll<DbContextOptions<CosmosDbContext>>();
        services.AddDbContext<CosmosDbContext>(options =>
        {
            options.UseCosmos(GetConnectionString(container), _dbName, cosmosOptions =>
            {
                cosmosOptions.ConnectionMode(Microsoft.Azure.Cosmos.ConnectionMode.Gateway);
                cosmosOptions.HttpClientFactory(() => container.HttpClient);
            });
        });
    }
}

public sealed class CosmosDbContainer : DockerContainer, IDatabaseContainer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbContainer" /> class.
    /// </summary>
    /// <param name="configuration">The container configuration.</param>
    public CosmosDbContainer(CosmosDbConfiguration configuration)
        : base(configuration)
    {
    }

    /// <summary>
    /// Gets the CosmosDb connection string.
    /// </summary>
    /// <returns>The CosmosDb connection string.</returns>
    public string GetConnectionString()
    {
        var properties = new Dictionary<string, string>
        {
            { "AccountEndpoint", new UriBuilder(Uri.UriSchemeHttp, Hostname, GetMappedPublicPort(CosmosDbBuilder.CosmosDbPort)).ToString() },
            { "AccountKey", CosmosDbBuilder.DefaultAccountKey }
        };
        return string.Join(";", properties.Select(property => string.Join("=", property.Key, property.Value)));
    }

    /// <summary>
    /// Gets a configured HTTP message handler that automatically trusts the CosmosDb Emulator's certificate.
    /// </summary>
    public HttpMessageHandler HttpMessageHandler => new UriRewriter(Hostname, GetMappedPublicPort(CosmosDbBuilder.CosmosDbPort));

    /// <summary>
    /// Gets a configured HTTP client that automatically trusts the CosmosDb Emulator's certificate.
    /// </summary>
    public HttpClient HttpClient => new HttpClient(HttpMessageHandler);

    /// <summary>
    /// Rewrites the HTTP requests to target the running CosmosDb Emulator instance.
    /// </summary>
    private sealed class UriRewriter : DelegatingHandler
    {
        private readonly string _hostname;

        private readonly ushort _port;

        /// <summary>
        /// Initializes a new instance of the <see cref="UriRewriter" /> class.
        /// </summary>
        /// <param name="hostname">The target hostname.</param>
        /// <param name="port">The target port.</param>
        public UriRewriter(string hostname, ushort port)
            : base(new HttpClientHandler())
        {
            _hostname = hostname;
            _port = port;
        }

        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.RequestUri = new UriBuilder(Uri.UriSchemeHttp, _hostname, _port, request.RequestUri!.PathAndQuery).Uri;
            return base.SendAsync(request, cancellationToken);
        }
    }
}
public sealed class CosmosDbBuilder : ContainerBuilder<CosmosDbBuilder, CosmosDbContainer, CosmosDbConfiguration>
{
    public const string CosmosDbImage = "mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview";

    public const ushort CosmosDbPort = 8081;

    public const string DefaultAccountKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbBuilder" /> class.
    /// </summary>
    public CosmosDbBuilder()
        : this(new CosmosDbConfiguration())
    {
        DockerResourceConfiguration = Init().DockerResourceConfiguration;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbBuilder" /> class.
    /// </summary>
    /// <param name="resourceConfiguration">The Docker resource configuration.</param>
    private CosmosDbBuilder(CosmosDbConfiguration resourceConfiguration)
        : base(resourceConfiguration)
    {
        DockerResourceConfiguration = resourceConfiguration;
    }

    /// <inheritdoc />
    protected override CosmosDbConfiguration DockerResourceConfiguration { get; }

    /// <inheritdoc />
    public override CosmosDbContainer Build()
    {
        Validate();
        return new CosmosDbContainer(DockerResourceConfiguration);
    }

    /// <inheritdoc />
    protected override CosmosDbBuilder Init()
    {
        return base.Init()
            .WithImage(CosmosDbImage)
            //.WithEnvironment("ENABLE_EXPLORER", "false")
            .WithPortBinding(CosmosDbPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer().AddCustomWaitStrategy(new WaitUntil()));
    }

    /// <inheritdoc />
    protected override CosmosDbBuilder Clone(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
    {
        return Merge(DockerResourceConfiguration, new CosmosDbConfiguration(resourceConfiguration));
    }

    /// <inheritdoc />
    protected override CosmosDbBuilder Clone(IContainerConfiguration resourceConfiguration)
    {
        return Merge(DockerResourceConfiguration, new CosmosDbConfiguration(resourceConfiguration));
    }

    /// <inheritdoc />
    protected override CosmosDbBuilder Merge(CosmosDbConfiguration oldValue, CosmosDbConfiguration newValue)
    {
        return new CosmosDbBuilder(new CosmosDbConfiguration(oldValue, newValue));
    }

    /// <inheritdoc cref="IWaitUntil" />
    private sealed class WaitUntil : IWaitUntil
    {
        /// <inheritdoc />
        public async Task<bool> UntilAsync(IContainer container)
        {
            // CosmosDB's preconfigured HTTP client will redirect the request to the container.
            const string REQUEST_URI = "http://localhost";

            using var httpClient = ((CosmosDbContainer)container).HttpClient;

            try
            {
                using var httpResponse = await httpClient.GetAsync(REQUEST_URI)
                    .ConfigureAwait(false);

                if (httpResponse.IsSuccessStatusCode)
                {
                    await Task.Delay(2_000);
                    return true;
                }
            }
            catch { }

            return false;
        }
    }
}
