namespace Vulthil.xUnit.Fixtures;

/// <summary>
/// Represents a test container that can be started and stopped as part of the test lifecycle.
/// </summary>
public interface ITestContainer : IAsyncLifetime;
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
    /// Gets the configuration key name where the connection string should be injected (e.g., "ConnectionStrings:MyDb").
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
