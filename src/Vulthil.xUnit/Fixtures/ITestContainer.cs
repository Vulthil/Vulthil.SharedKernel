

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
/// Extends <see cref="ITestContainerWithConnectionString"/> with database migration and reset capabilities.
/// </summary>
public interface ITestDatabaseContainer : ITestContainerWithConnectionString
{
    /// <summary>
    /// Initializes the database respawner for resetting data between tests.
    /// </summary>
    ValueTask InitializeRespawner();
    /// <summary>
    /// Applies pending EF Core migrations to the containerized database.
    /// </summary>
    /// <param name="serviceProvider">The scoped service provider to resolve the DbContext from.</param>
    ValueTask MigrateDatabase(IServiceProvider serviceProvider);
    /// <summary>
    /// Resets the database to a clean state by removing all data.
    /// </summary>
    ValueTask ResetDatabase();
}
