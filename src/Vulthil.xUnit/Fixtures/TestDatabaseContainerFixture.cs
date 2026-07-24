using System.Data.Common;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Testcontainers.Xunit;
using Xunit.Sdk;

namespace Vulthil.xUnit.Fixtures;

/// <summary>
/// Fixture that wraps a database container, applies EF Core migrations, and supports Respawn-based data resets between
/// tests. When owned by a <see cref="ContainerHost"/>, <see cref="CreateScope"/> gives every consuming factory its own
/// uniquely named database on the shared server, so parallel test classes never see each other's data.
/// </summary>
public abstract class TestDatabaseContainerFixture<TDbContext, TBuilderEntity, TContainerEntity>(IMessageSink messageSink)
    : DbContainerFixture<TBuilderEntity, TContainerEntity>(messageSink), ITestDatabaseContainer, ITestContainerScopeProvider
    where TDbContext : DbContext
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer, IDatabaseContainer
{
#pragma warning disable IDE0032 // Created lazily because ConnectionString requires a started container.
    private DatabaseScope? _defaultScope;
#pragma warning restore IDE0032
    private DatabaseScope DefaultScope => _defaultScope ??= new DatabaseScope(this, ConnectionString, databaseName: null);

    /// <summary>
    /// Gets the Respawn database adapter matching the container's database engine.
    /// </summary>
    protected abstract IDbAdapter DbAdapter { get; }
    /// <summary>
    /// Gets the configuration key name where the connection string should be injected.
    /// </summary>
    public abstract string ConnectionStringKey { get; }

    /// <inheritdoc />
    public virtual void ConfigureWebHost(IWebHostBuilder builder)
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// The base implementation decorates the application's <see cref="DbContextOptions{TContext}"/> registration to
    /// ignore <see cref="CoreEventId.ManyServiceProvidersCreatedWarning"/>. With shared containers every test class
    /// fixture (and every per-test <c>WithWebHostBuilder</c> clone) builds its own host, and therefore its own EF Core
    /// internal service provider, so large suites trip that warning even though each host is correct. Overrides should
    /// call <see langword="base"/> to preserve this behavior.
    /// </remarks>
    public virtual void ConfigureServices(IServiceCollection services) => IgnoreManyServiceProvidersWarning(services);

    private static void IgnoreManyServiceProvidersWarning(IServiceCollection services)
    {
        var optionsDescriptor = services.LastOrDefault(service => service.ServiceType == typeof(DbContextOptions<TDbContext>));
        if (optionsDescriptor?.ImplementationFactory is not { } originalFactory)
        {
            return;
        }

        services.Remove(optionsDescriptor);
        services.Add(new ServiceDescriptor(
            optionsDescriptor.ServiceType,
            serviceProvider => new DbContextOptionsBuilder<TDbContext>((DbContextOptions<TDbContext>)originalFactory(serviceProvider))
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options,
            optionsDescriptor.Lifetime));
    }

    /// <inheritdoc />
    public ValueTask MigrateDatabase(IServiceProvider serviceProvider) => DefaultScope.MigrateDatabase(serviceProvider);

    /// <inheritdoc />
    public ValueTask ResetAsync(IServiceProvider serviceProvider) => DefaultScope.ResetAsync(serviceProvider);

    /// <summary>
    /// Creates a scope view backed by its own database on this server, named after <paramref name="scopeId"/>. The
    /// view creates the database when it initializes, applies migrations through
    /// <see cref="ITestDatabaseContainer.MigrateDatabase"/>, resets it with Respawn between tests, and drops it
    /// (best-effort) when disposed. The server container itself keeps running for other scopes.
    /// </summary>
    /// <param name="scopeId">A short, unique, lowercase identifier for the scope; sanitized into a database name.</param>
    /// <returns>The scoped container view.</returns>
    public virtual ITestContainer CreateScope(string scopeId)
    {
        var databaseName = SanitizeDatabaseName(scopeId);
        return new DatabaseScope(this, BuildScopedConnectionString(databaseName), databaseName);
    }

    /// <summary>
    /// Builds a connection string pointing at <paramref name="databaseName"/> on this server by rewriting the
    /// container's default connection string. Override when the provider does not use the <c>Database</c> keyword.
    /// </summary>
    /// <param name="databaseName">The database the returned connection string should target.</param>
    /// <returns>The scoped connection string.</returns>
    protected virtual string BuildScopedConnectionString(string databaseName)
    {
        ArgumentException.ThrowIfNullOrEmpty(databaseName);

        var connectionStringBuilder = DbProviderFactory.CreateConnectionStringBuilder()
            ?? throw new InvalidOperationException(
                $"'{DbProviderFactory.GetType().Name}' does not supply a DbConnectionStringBuilder; override {nameof(BuildScopedConnectionString)}.");
        connectionStringBuilder.ConnectionString = ConnectionString;
        connectionStringBuilder["Database"] = databaseName;
        return connectionStringBuilder.ConnectionString;
    }

    /// <summary>
    /// Creates the database <paramref name="databaseName"/> on the server, using a connection to the container's
    /// default database. Override for engines that need non-standard DDL.
    /// </summary>
    /// <param name="databaseName">The name of the database to create.</param>
    /// <returns>A task representing the asynchronous work.</returns>
    protected virtual Task CreateDatabaseAsync(string databaseName) =>
        ExecuteServerCommandAsync($"CREATE DATABASE {SanitizeDatabaseName(databaseName)}");

    /// <summary>
    /// Drops the database <paramref name="databaseName"/> on the server. Invoked best-effort when a scope is
    /// disposed; failures are reported as diagnostics rather than test failures, because the server container is
    /// torn down with the host anyway. The default issues engine-appropriate DDL based on <see cref="DbAdapter"/> —
    /// PostgreSQL terminates lingering (pooled) sessions with <c>WITH (FORCE)</c>, SQL Server switches the database
    /// to single-user first — so consumers normally never override this.
    /// </summary>
    /// <param name="databaseName">The name of the database to drop.</param>
    /// <returns>A task representing the asynchronous work.</returns>
    protected virtual Task DropDatabaseAsync(string databaseName)
    {
        var name = SanitizeDatabaseName(databaseName);

        var commandText = DbAdapter switch
        {
            var adapter when ReferenceEquals(adapter, Respawn.DbAdapter.Postgres) =>
                $"DROP DATABASE IF EXISTS {name} WITH (FORCE)",
            var adapter when ReferenceEquals(adapter, Respawn.DbAdapter.SqlServer) =>
                $"IF DB_ID('{name}') IS NOT NULL BEGIN ALTER DATABASE {name} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {name}; END",
            _ => $"DROP DATABASE IF EXISTS {name}",
        };

        return ExecuteServerCommandAsync(commandText);
    }

    /// <summary>
    /// Executes a single DDL command against the container's default database.
    /// </summary>
    /// <param name="commandText">The command text to execute.</param>
    /// <returns>A task representing the asynchronous work.</returns>
    protected async Task ExecuteServerCommandAsync(string commandText)
    {
        ArgumentException.ThrowIfNullOrEmpty(commandText);

        var connection = await OpenConnectionAsync().ConfigureAwait(false);
        await using var connectionDisposer = connection.ConfigureAwait(false);

        var command = connection.CreateCommand();
        await using var commandDisposer = command.ConfigureAwait(false);
#pragma warning disable CA2100 // DDL against the test container; identifiers are sanitized and cannot be parameterized.
        command.CommandText = commandText;
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Normalizes <paramref name="scopeId"/> into an identifier that is safe to use unquoted as a database name on
    /// any supported engine: lowercase ASCII letters, digits and underscores, starting with a letter, at most 63
    /// characters.
    /// </summary>
    /// <param name="scopeId">The scope identifier to normalize.</param>
    /// <returns>The sanitized database name.</returns>
    protected static string SanitizeDatabaseName(string scopeId)
    {
        ArgumentException.ThrowIfNullOrEmpty(scopeId);

        var sanitized = new string([.. scopeId.Select(c => char.IsAsciiLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_')]);
        if (!char.IsAsciiLetter(sanitized[0]))
        {
            sanitized = $"db_{sanitized}";
        }

        return sanitized.Length <= 63 ? sanitized : sanitized[..63];
    }

    private sealed class DatabaseScope(
        TestDatabaseContainerFixture<TDbContext, TBuilderEntity, TContainerEntity> fixture,
        string connectionString,
        string? databaseName) : ITestDatabaseContainer
    {
        private const int MaxMigrationAttempts = 10;
        private const int MigrationRetryDelayMilliseconds = 250;

        private Respawner? _respawner;
        private bool _hasBeenMigrated;

        public string ConnectionString => connectionString;

        public string ConnectionStringKey => fixture.ConnectionStringKey;

        public void ConfigureWebHost(IWebHostBuilder builder) => fixture.ConfigureWebHost(builder);

        public void ConfigureServices(IServiceCollection services) => fixture.ConfigureServices(services);

        public async ValueTask InitializeAsync()
        {
            if (databaseName is not null)
            {
                await fixture.CreateDatabaseAsync(databaseName).ConfigureAwait(false);
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (databaseName is null)
            {
                return;
            }

            try
            {
                await fixture.DropDatabaseAsync(databaseName).ConfigureAwait(false);
            }
            catch (DbException exception)
            {
                TestContext.Current.SendDiagnosticMessage(
                    $"Dropping scoped database '{databaseName}' failed; it is removed with the container: {exception.Message}");
            }
        }

        public async ValueTask MigrateDatabase(IServiceProvider serviceProvider)
        {
            if (_hasBeenMigrated)
            {
                return;
            }

            var dbContext = serviceProvider.GetRequiredService<TDbContext>();

            for (var attempt = 1; attempt <= MaxMigrationAttempts; attempt++)
            {
                if (!(await dbContext.Database.GetPendingMigrationsAsync().ConfigureAwait(false)).Any())
                {
                    _hasBeenMigrated = true;
                    return;
                }

                try
                {
                    await dbContext.Database.MigrateAsync().ConfigureAwait(false);
                    _hasBeenMigrated = true;
                    return;
                }
                catch when (attempt < MaxMigrationAttempts)
                {
                    await Task.Delay(MigrationRetryDelayMilliseconds).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException(
                $"Pending migrations for '{typeof(TDbContext).Name}' did not complete after {MaxMigrationAttempts} attempts.");
        }

        public async ValueTask ResetAsync(IServiceProvider serviceProvider)
        {
            if (!_hasBeenMigrated)
            {
                return;
            }

            var respawner = await GetOrCreateRespawnerAsync().ConfigureAwait(false);

            var connection = await OpenScopedConnectionAsync().ConfigureAwait(false);
            await using var _ = connection.ConfigureAwait(false);
            await respawner.ResetAsync(connection).ConfigureAwait(false);
        }

        private async ValueTask<Respawner> GetOrCreateRespawnerAsync()
        {
            if (_respawner is not null)
            {
                return _respawner;
            }

            var connection = await OpenScopedConnectionAsync().ConfigureAwait(false);
            await using var _ = connection.ConfigureAwait(false);
            _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = fixture.DbAdapter,
                WithReseed = true,
                TablesToIgnore = ["__EFMigrationsHistory"],
            }).ConfigureAwait(false);
            return _respawner;
        }

        private async Task<DbConnection> OpenScopedConnectionAsync()
        {
            var connection = fixture.DbProviderFactory.CreateConnection()
                ?? throw new InvalidOperationException(
                    $"'{fixture.DbProviderFactory.GetType().Name}' does not supply a DbConnection.");

            try
            {
                connection.ConnectionString = connectionString;
                await connection.OpenAsync().ConfigureAwait(false);
                return connection;
            }
            catch
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
    }
}
