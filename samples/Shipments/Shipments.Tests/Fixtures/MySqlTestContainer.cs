using System.Data.Common;
using MySqlConnector;
using Respawn;
using ServiceDefaults;
using Shipments.Infrastructure;
using Testcontainers.MySql;
using Vulthil.xUnit.Fixtures;

namespace Shipments.Tests.Fixtures;

/// <summary>
/// Runs as root, because every factory scope creates and drops a database of its own on the shared server.
/// </summary>
internal sealed class MySqlTestContainer : TestDatabaseContainerFixture<ShipmentsDbContext, MySqlBuilder, MySqlContainer>
{
    private readonly MySqlBuilder _builder = new MySqlBuilder("mysql:8.4")
        .WithUsername("root")
        .WithPassword("shipments");

    protected override MySqlBuilder Configure() => _builder;

    protected override IDbAdapter DbAdapter => Respawn.DbAdapter.MySql;

    public override DbProviderFactory DbProviderFactory => MySqlConnectorFactory.Instance;

    public override string ConnectionStringKey => ServiceNames.ShipmentsDatabaseServiceName;
}
