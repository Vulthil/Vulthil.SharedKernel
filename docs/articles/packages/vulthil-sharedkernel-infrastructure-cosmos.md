# Vulthil.SharedKernel.Infrastructure.Cosmos

Use `Vulthil.SharedKernel.Infrastructure.Cosmos` to run the shared infrastructure (including outbox) against Azure Cosmos DB.

## When to use

- Cosmos DB is the underlying store for an application's primary `DbContext`
- Aspire-wired Cosmos connection via `AddCosmosDbContext`
- Outbox processing should use the Cosmos-specific strategy (best-effort relay without relational locking)

## Pattern

- Call `UseCosmosDb("connectionStringKey")` on the database infrastructure configurator – it both registers the EF Core context and selects the Cosmos outbox strategy
- Configure the Cosmos-specific entity model for `OutboxMessage` via the `ApplyCosmosOutbox()` model-builder extension (call it from your `OnModelCreating`)
- Order between `UseCosmosDb`, `EnableOutboxProcessing`, and `UseOutboxStore` does not matter; the configurator defers the underlying registrations until the full chain has executed, and the Cosmos outbox store is applied only as a default – a custom store selected via `UseOutboxStore` is always preserved

## Usage

### Registration

```csharp
builder.AddDbContext<AppDbContext>(config => config
    .UseCosmosDb("Cosmos", settings =>
    {
        settings.DatabaseName = "app";
    })
    .EnableOutboxProcessing(o =>
    {
        o.BatchSize = 20;
        o.MaxRetries = 3;
    }));
```

### Notes on Cosmos behavior

- Cosmos DB has no relay-wide transaction, so the Cosmos outbox store runs the relay batch without one: pending messages are fetched with the provider-agnostic query and their outcome is recorded through tracked updates in a single save. Delivery is at-least-once, as with every outbox provider.
- The relay fetch orders pending messages by `OccurredOnUtc` then `Id`. Azure Cosmos DB documents multi-property `ORDER BY` as requiring a composite index, which the default EF mapping does not create; the emulator serves this query on a container created with default indexing (pinned by the integration suite), but if the Azure service rejects the relay query or it becomes expensive, add a composite index over `(OccurredOnUtc, id)` to the outbox container's indexing policy.
- Migrations do not apply to Cosmos – use `EnsureCreatedAsync<AppDbContext>()` instead of `MigrateAsync<AppDbContext>()` during development setup.
