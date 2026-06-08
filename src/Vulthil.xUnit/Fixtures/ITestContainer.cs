using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Vulthil.xUnit.Fixtures;

/// <summary>
/// Represents a test container that can be started and stopped as part of the test lifecycle, and that may
/// participate in configuring the test host.
/// </summary>
public interface ITestContainer : IAsyncLifetime
{
    /// <summary>
    /// Configures the test host for this container. Invoked by <see cref="BaseWebApplicationFactory{TEntryPoint}"/>
    /// during host build, after connection strings are injected and before the factory's own
    /// <c>ConfigureCustomWebHost</c>. Use it to apply host settings the container needs.
    /// </summary>
    /// <param name="builder">The web host builder to configure.</param>
    void ConfigureWebHost(IWebHostBuilder builder);

    /// <summary>
    /// Registers or replaces services for this container in the test host. Invoked through
    /// <c>ConfigureTestServices</c>, so it runs after the application's own registrations and can decorate or
    /// replace them (for example, repointing a <c>DbContext</c> at the containerized service).
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    void ConfigureServices(IServiceCollection services);
}
/// <summary>
/// Extends <see cref="ITestContainer"/> with a connection string for use in integration tests.
/// </summary>
public interface ITestContainerWithConnectionString : ITestContainer
{
    /// <summary>
    /// Gets the connection string used to communicate with the containerized service.
    /// </summary>
    string ConnectionString { get; }
    /// <summary>
    /// Gets the bare configuration key name where the connection string should be injected (e.g., "MyDb"); the factory binds it under <c>ConnectionStrings:{ConnectionStringKey}</c>.
    /// </summary>
    string ConnectionStringKey { get; }
}
/// <summary>
/// Extends <see cref="ITestContainerWithConnectionString"/> with database migration capabilities. Data is reset
/// between tests through <see cref="IResettableResource.ResetAsync"/> (Respawn).
/// </summary>
public interface ITestDatabaseContainer : ITestContainerWithConnectionString, IResettableResource
{
    /// <summary>
    /// Ensures pending EF Core migrations are applied to the containerized database. Invoked during host startup,
    /// before the application's own background services run, so the schema exists in time; only migrations that remain
    /// pending are applied, and a concurrent migrator (such as an application that migrates itself) is tolerated rather
    /// than fought.
    /// </summary>
    /// <param name="serviceProvider">The scoped service provider to resolve the DbContext from.</param>
    /// <returns>A task representing the asynchronous migration work.</returns>
    ValueTask MigrateDatabase(IServiceProvider serviceProvider);
}
