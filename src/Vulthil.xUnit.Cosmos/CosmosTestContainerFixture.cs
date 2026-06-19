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
/// <remarks>
/// The database is created and reset through the application's own <typeparamref name="TDbContext"/> resolved from the
/// test host's service provider — so a context whose constructor takes more than its options works without extra
/// plumbing. It is created once during host startup through <see cref="IStartupResource.InitializeAsync"/> and
/// recreated after each test through <see cref="IResettableResource.ResetAsync"/>. Only the emulator readiness probe
/// and the best-effort scope teardown use a bare <see cref="DbContext"/>, because neither needs the model.
/// </remarks>
/// <typeparam name="TDbContext">The Cosmos-mapped <see cref="DbContext"/> to register against the emulator.</typeparam>
/// <param name="messageSink">The xUnit diagnostic message sink.</param>
public abstract class CosmosTestContainerFixture<TDbContext>(IMessageSink messageSink)
    : TestContainerFixtureWithConnectionString<CosmosDbBuilder, CosmosDbContainer>(messageSink), IStartupResource, IResettableResource
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

    /// <inheritdoc />
    public ValueTask InitializeAsync(IServiceProvider serviceProvider) => EnsureDatabaseCreatedAsync(serviceProvider);

    /// <summary>
    /// Drops and recreates the default database after each test, giving every test a clean slate. The
    /// <typeparamref name="TDbContext"/> is resolved from <paramref name="serviceProvider"/>, so its model — and any
    /// constructor dependencies — come from the test host rather than being constructed here.
    /// </summary>
    /// <param name="serviceProvider">The application's root service provider.</param>
    /// <returns>A task representing the asynchronous reset work.</returns>
    public ValueTask ResetAsync(IServiceProvider serviceProvider) => RecreateDatabaseAsync(serviceProvider);

    /// <summary>
    /// Creates a scope backed by its own database inside the shared emulator, named after
    /// <paramref name="scopeId"/>. The scope's database is created during host startup, recreated between tests, and
    /// deleted (best-effort) when disposed; the emulator container keeps running for other scopes.
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

    private static async ValueTask EnsureDatabaseCreatedAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    private static async ValueTask RecreateDatabaseAsync(IServiceProvider serviceProvider)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
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

    private DbContext CreateProbeContext(string databaseName)
    {
        var optionsBuilder = new DbContextOptionsBuilder();
        ApplyCosmosOptions(optionsBuilder, databaseName);
        return new DbContext(optionsBuilder.Options);
    }

    private async ValueTask WaitForEmulatorAsync()
    {
        for (var attempt = 1; attempt <= MaxReadinessAttempts; attempt++)
        {
            try
            {
                await using var context = CreateProbeContext(DatabaseName);
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

    private async ValueTask DropDatabaseAsync(string databaseName)
    {
        await using var context = CreateProbeContext(databaseName);
        await context.Database.EnsureDeletedAsync();
    }

    private sealed class CosmosDatabaseScope(CosmosTestContainerFixture<TDbContext> fixture, string databaseName)
        : ITestContainerWithConnectionString, IStartupResource, IResettableResource
    {
        public string ConnectionString => fixture.ConnectionString;

        public string ConnectionStringKey => fixture.ConnectionStringKey;

        public void ConfigureWebHost(IWebHostBuilder builder) => fixture.ConfigureWebHost(builder);

        public void ConfigureServices(IServiceCollection services) =>
            services.AddDbContext<TDbContext>(options => fixture.ApplyCosmosOptions(options, databaseName));

        public ValueTask InitializeAsync() => ValueTask.CompletedTask;

        public ValueTask InitializeAsync(IServiceProvider serviceProvider) => EnsureDatabaseCreatedAsync(serviceProvider);

        public ValueTask ResetAsync(IServiceProvider serviceProvider) => RecreateDatabaseAsync(serviceProvider);

        public async ValueTask DisposeAsync()
        {
            try
            {
                await fixture.DropDatabaseAsync(databaseName);
            }
            catch (Exception exception)
            {
                TestContext.Current.SendDiagnosticMessage(
                    $"Deleting scoped Cosmos database '{databaseName}' failed; it is removed with the container: {exception.Message}");
            }
        }
    }
}
