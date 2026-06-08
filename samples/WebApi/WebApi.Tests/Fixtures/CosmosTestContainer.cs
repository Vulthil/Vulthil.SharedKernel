using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ServiceDefaults;
using Testcontainers.CosmosDb;
using Vulthil.xUnit;
using Vulthil.xUnit.Fixtures;
using Xunit.Sdk;

namespace WebApi.Tests.Fixtures;

/// <summary>
/// Starts the Azure Cosmos DB Linux emulator in a container and registers <typeparamref name="TDbContext"/> in the
/// test host pointed at it. Connectivity goes through the container's own <see cref="CosmosDbContainer.HttpClient"/>,
/// whose handler rewrites every request to the mapped emulator endpoint, so no certificate or endpoint plumbing is
/// needed here. Data is reset between tests by recreating the database.
/// </summary>
internal sealed class CosmosTestContainer<TDbContext>(IMessageSink messageSink)
    : TestContainerFixtureWithConnectionString<CosmosDbBuilder, CosmosDbContainer>(messageSink), IResettableResource
    where TDbContext : DbContext
{
    private const string CosmosDbImage = "mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-latest";
    private const string DatabaseName = "test-db";
    private const int MaxReadinessAttempts = 60;
    private const int ReadinessRetryDelaySeconds = 1;

    private readonly CosmosDbBuilder _builder = new(CosmosDbImage);

    protected override CosmosDbBuilder Configure() => _builder;

    public override string ConnectionStringKey => ServiceNames.CosmosDbServiceName;

    public override string ConnectionString => Container.GetConnectionString();

    /// <summary>
    /// Registers <typeparamref name="TDbContext"/> in the test host, pointed at the emulator. Invoked through the
    /// container extension point when this fixture is added to a <see cref="BaseWebApplicationFactory{TEntryPoint}"/>.
    /// </summary>
    public override void ConfigureServices(IServiceCollection services) =>
        services.AddDbContext<TDbContext>(ApplyCosmosOptions);

    /// <summary>
    /// Drops and recreates the database, giving each test a clean slate.
    /// </summary>
    public async ValueTask ResetAsync()
    {
        await using var context = CreateStandaloneDbContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }

    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await WaitForEmulatorAsync();
    }

    private TDbContext CreateStandaloneDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();
        ApplyCosmosOptions(optionsBuilder);
        return (TDbContext)Activator.CreateInstance(typeof(TDbContext), optionsBuilder.Options)!;
    }

    private void ApplyCosmosOptions(DbContextOptionsBuilder options)
    {
        options.UseCosmos(ConnectionString, DatabaseName, cosmos =>
        {
            cosmos.ConnectionMode(ConnectionMode.Gateway);
            cosmos.HttpClientFactory(() => Container.HttpClient);
        });
        options.ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
    }

    private async ValueTask WaitForEmulatorAsync()
    {
        for (var attempt = 1; attempt <= MaxReadinessAttempts; attempt++)
        {
            try
            {
                await using var context = CreateStandaloneDbContext();
                await context.Database.EnsureCreatedAsync();
                return;
            }
            catch (CosmosException) when (attempt < MaxReadinessAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(ReadinessRetryDelaySeconds));
            }
            catch (HttpRequestException) when (attempt < MaxReadinessAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(ReadinessRetryDelaySeconds));
            }
        }
    }
}
