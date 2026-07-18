using System.Data.Common;
using MySqlConnector;
using Respawn;
using Testcontainers.MySql;
using Vulthil.xUnit.Fixtures;
using Xunit.Sdk;

namespace Vulthil.IntegrationTests.Fixtures;

/// <summary>
/// Shared MySQL server for the provider outbox tests. Runs as root so per-class scope views can create and drop
/// their own databases on the shared server, mirroring the PostgreSQL fixture.
/// </summary>
internal sealed class MySqlTestContainer(IMessageSink messageSink) : TestDatabaseContainerFixture<MySqlOutboxDbContext, MySqlBuilder, MySqlContainer>(messageSink)
{
    public const string ConnectionStringKeyName = "mysql";

    private readonly MySqlBuilder _builder = new MySqlBuilder("mysql:8.4")
        .WithUsername("root")
        .WithPassword("vulthil");

    protected override MySqlBuilder Configure() => _builder;

    protected override IDbAdapter DbAdapter => Respawn.DbAdapter.MySql;
    public override DbProviderFactory DbProviderFactory => MySqlConnectorFactory.Instance;
    public override string ConnectionStringKey => ConnectionStringKeyName;
}
