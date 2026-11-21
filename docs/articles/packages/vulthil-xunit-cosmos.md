# Vulthil.xUnit.Cosmos

Use `Vulthil.xUnit.Cosmos` to test against the Azure Cosmos DB emulator with `Vulthil.xUnit`.

## When to use

- Integration tests for Cosmos-mapped EF Core contexts
- Sharing one emulator container across parallel test classes via a `ContainerHost`, with an emulator database per class

## Pattern

- Derive `CosmosTestContainerFixture<TDbContext>` and supply the configuration key (optionally pin the emulator image via `Configure`)
- Register the fixture on the assembly's `ContainerHost` (or a factory) like any other container
- Let the fixture own database creation, per-test resets, and per-scope isolation — no scope plumbing in test code
- A host that registers its context with `UseCosmosDb` works as it is: the scope's connection string names the scope's database, and the scope points the host's (pooled) context registration at the emulator

See [Testing](../testing.md) for the full integration-testing guide.
