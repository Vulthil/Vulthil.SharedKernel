namespace Vulthil.TestHost;

/// <summary>
/// Connection-string keys the host reads its infrastructure configuration from; test fixtures inject their
/// container connection strings under these keys.
/// </summary>
public static class TestHostConnectionStrings
{
    public const string Postgres = "postgres";
    public const string RabbitMq = "rabbitMq";
    public const string CosmosDb = "cosmosdb";
}
