namespace WebApi.Tests.Fixtures;

/// <summary>
/// Shares a single <see cref="CosmosWebApplicationFactory"/> (and therefore one Cosmos emulator container) across
/// every Cosmos test class, so the costly emulator starts only once for the whole suite.
/// </summary>
[CollectionDefinition(Name)]
public sealed class CosmosCollection : ICollectionFixture<CosmosWebApplicationFactory>
{
    public const string Name = "Cosmos";
}
