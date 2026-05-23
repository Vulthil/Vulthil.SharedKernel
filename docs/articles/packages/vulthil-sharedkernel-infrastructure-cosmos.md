# Vulthil.SharedKernel.Infrastructure.Cosmos

Use `Vulthil.SharedKernel.Infrastructure.Cosmos` to run the shared infrastructure (including outbox) against Azure Cosmos DB.

## When to use

- Cosmos DB is the underlying store for an application's primary `DbContext`
- Aspire-wired Cosmos connection via `AddCosmosDbContext`
- Outbox processing should use the Cosmos-specific strategy (transactional batches per partition rather than relational locking)

## Pattern

- Call `UseCosmosDb("connectionName")` on the database infrastructure configurator – it both registers the EF Core context and selects the Cosmos outbox strategy
- Configure the Cosmos-specific entity model for `OutboxMessage` via `OutboxMessageEntityConfiguration` (applied automatically when `EnableOutboxProcessing` runs)
- Order between `UseCosmosDb` and `EnableOutboxProcessing` does not matter; the configurator defers the underlying call until the full chain has executed

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

- The Cosmos outbox strategy uses Cosmos transactional batches, which require all participating documents to share a partition key. If you customize the outbox entity configuration, keep the partition key choice consistent with how messages are produced.
- Migrations do not apply to Cosmos – use `EnsureCreatedAsync<AppDbContext>()` instead of `MigrateAsync<AppDbContext>()` during development setup.
