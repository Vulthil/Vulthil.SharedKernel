using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Vulthil.IntegrationTests.Fixtures;
using Vulthil.xUnit;
using Vulthil.xUnit.Fixtures;

namespace Vulthil.IntegrationTests;

/// <summary>
/// A MySQL database is a schema, and the information schema of the shared server lists the tables of every scope's
/// database. These tests pin that the per-test reset of one scope empties only that scope's database.
/// </summary>
public sealed class MySqlScopeResetTests(IntegrationTestContainerHost containerHost) : BaseUnitTestCase
{
    [Fact]
    public async Task ResettingOneScopeLeavesTheDatabaseOfAnotherScopeUntouched()
    {
        // Arrange
        var container = containerHost.Containers.OfType<MySqlTestContainer>().Single();
        await containerHost.EnsureStartedAsync(container);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var resetScope = (ITestDatabaseContainer)container.CreateScope($"reset_{suffix}");
        var keptScope = (ITestDatabaseContainer)container.CreateScope($"kept_{suffix}");
        await using var resetLifetime = resetScope;
        await using var keptLifetime = keptScope;
        await resetScope.InitializeAsync();
        await keptScope.InitializeAsync();

        await CreateProbeTableWithOneRowAsync(resetScope.ConnectionString);
        await CreateProbeTableWithOneRowAsync(keptScope.ConnectionString);

        await using var services = new ServiceCollection()
            .AddDbContext<MySqlOutboxDbContext>(options => options.UseMySql(resetScope.ConnectionString, ServerVersion.Parse("8.4.0-mysql")))
            .BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        await resetScope.MigrateDatabaseAsync(scope.ServiceProvider, CancellationToken);

        // Act
        await resetScope.ResetAsync(scope.ServiceProvider, CancellationToken);

        // Assert
        (await CountProbeRowsAsync(resetScope.ConnectionString)).ShouldBe(0);
        (await CountProbeRowsAsync(keptScope.ConnectionString)).ShouldBe(1);
    }

    private static async Task CreateProbeTableWithOneRowAsync(string connectionString)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new MySqlCommand("CREATE TABLE ResetProbe (Id INT PRIMARY KEY); INSERT INTO ResetProbe (Id) VALUES (1);", connection);
        await command.ExecuteNonQueryAsync(CancellationToken);
    }

    private static async Task<long> CountProbeRowsAsync(string connectionString)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = new MySqlCommand("SELECT COUNT(*) FROM ResetProbe;", connection);
        return (long)(await command.ExecuteScalarAsync(CancellationToken))!;
    }
}
