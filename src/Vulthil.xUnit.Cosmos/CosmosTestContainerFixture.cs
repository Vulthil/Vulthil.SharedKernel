using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.CosmosDb;
using Vulthil.xUnit.Fixtures;
using Xunit;
using Xunit.Sdk;

namespace Vulthil.xUnit.Cosmos;

/// <summary>
/// Fixture that starts the Azure Cosmos DB Linux emulator in a container and registers
/// <typeparamref name="TDbContext"/> in the test host pointed at it. Connectivity goes through the container's own
/// <see cref="CosmosDbContainer.HttpClient"/>, whose handler rewrites every request to the mapped emulator endpoint,
/// so no certificate or endpoint plumbing is needed. When owned by a <see cref="ContainerHost"/>, every consuming
/// factory works in its own emulator database, so parallel test classes never share data; data is reset between
/// tests by recreating that database. Derived classes only supply the configuration key (and optionally pin the
/// emulator image via <c>Configure</c>).
/// </summary>
/// <typeparam name="TDbContext">The Cosmos-mapped <see cref="DbContext"/> to register against the emulator.</typeparam>
/// <param name="messageSink">The xUnit diagnostic message sink.</param>
public abstract class CosmosTestContainerFixture<TDbContext>(IMessageSink messageSink)
    : TestContainerFixtureWithConnectionString<CosmosDbBuilder, CosmosDbContainer>(messageSink), IResettableResource
    where TDbContext : DbContext
{
    private const string DefaultCosmosDbImage = "mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-latest";
    private const int MaxReadinessAttempts = 60;
    private const int ReadinessRetryDelaySeconds = 1;

    /// <summary>
    /// Gets the name of the emulator database used when this fixture is consumed without a scope; scopes append
    /// their scope identifier to it. Defaults to <c>test-db</c>.
    /// </summary>
    protected virtual string DatabaseName => "test-db";

    /// <summary>
    /// Configures the emulator container. The default uses the <c>vnext</c> emulator image; override to pin a
    /// specific image or tune the builder.
    /// </summary>
    /// <returns>The configured builder.</returns>
    protected override CosmosDbBuilder Configure() => new(DefaultCosmosDbImage);

    /// <inheritdoc />
    public override string ConnectionString => Container.GetConnectionString();

    /// <summary>
    /// Registers <typeparamref name="TDbContext"/> in the test host, pointed at the emulator's default database.
    /// Invoked through the container extension point when this fixture is consumed without a scope.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public override void ConfigureServices(IServiceCollection services) =>
        services.AddDbContext<TDbContext>(options => ApplyCosmosOptions(options, DatabaseName));

    /// <summary>
    /// Drops and recreates the default database, giving each test a clean slate.
    /// </summary>
    /// <returns>A task representing the asynchronous reset work.</returns>
    public ValueTask ResetAsync() => ResetDatabaseAsync(DatabaseName);

    /// <summary>
    /// Creates a scope backed by its own database inside the shared emulator, named after
    /// <paramref name="scopeId"/>. The scope creates the database when it initializes, recreates it between tests,
    /// and deletes it (best-effort) when disposed; the emulator container keeps running for other scopes.
    /// </summary>
    /// <param name="scopeId">A short, unique, lowercase identifier for the scope; appended to <see cref="DatabaseName"/>.</param>
    /// <returns>The scoped container view.</returns>
    public override ITestContainer CreateScope(string scopeId) => new CosmosDatabaseScope(this, $"{DatabaseName}-{scopeId}");

    /// <inheritdoc />
    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await WaitForEmulatorAsync();
    }

    private async ValueTask ResetDatabaseAsync(string databaseName)
    {
        await using var context = CreateStandaloneDbContext(databaseName);
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }

    private TDbContext CreateStandaloneDbContext(string databaseName)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();
        ApplyCosmosOptions(optionsBuilder, databaseName);
        return (TDbContext)Activator.CreateInstance(typeof(TDbContext), optionsBuilder.Options)!;
    }

    private void ApplyCosmosOptions(DbContextOptionsBuilder options, string databaseName)
    {
        options.UseCosmos(ConnectionString, databaseName, cosmos =>
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
                await using var context = CreateStandaloneDbContext(DatabaseName);
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

    private sealed class CosmosDatabaseScope(CosmosTestContainerFixture<TDbContext> fixture, string databaseName)
        : ITestContainerWithConnectionString, IResettableResource
    {
        public string ConnectionString => fixture.ConnectionString;

        public string ConnectionStringKey => fixture.ConnectionStringKey;

        public void ConfigureWebHost(IWebHostBuilder builder) => fixture.ConfigureWebHost(builder);

        public void ConfigureServices(IServiceCollection services) =>
            services.AddDbContext<TDbContext>(options => fixture.ApplyCosmosOptions(options, databaseName));

        public async ValueTask InitializeAsync()
        {
            await using var context = fixture.CreateStandaloneDbContext(databaseName);
            await context.Database.EnsureCreatedAsync();
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var context = fixture.CreateStandaloneDbContext(databaseName);
                await context.Database.EnsureDeletedAsync();
            }
            catch (Exception exception)
            {
                TestContext.Current.SendDiagnosticMessage(
                    $"Deleting scoped Cosmos database '{databaseName}' failed; it is removed with the container: {exception.Message}");
            }
        }

        public ValueTask ResetAsync() => fixture.ResetDatabaseAsync(databaseName);
    }
}
