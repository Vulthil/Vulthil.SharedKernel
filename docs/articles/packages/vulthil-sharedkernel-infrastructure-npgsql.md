# Vulthil.SharedKernel.Infrastructure.Npgsql

Use `Vulthil.SharedKernel.Infrastructure.Npgsql` to wire a PostgreSQL-backed `DbContext` together with the PostgreSQL-tuned outbox store.

## When to use

- PostgreSQL is the underlying database for an application's primary `DbContext`
- Aspire-wired connection strings via `AddNpgsqlDbContext`
- Outbox processing should use the PostgreSQL store (row-level locking via `FOR UPDATE SKIP LOCKED`)

## Pattern

- Call `UseNpgsql("ConnectionStringKey")` on the database infrastructure configurator – it both registers the EF Core context and selects the Npgsql outbox store
- Order between `UseNpgsql`, `EnableOutboxProcessing`, and `UseOutboxStore` does not matter; the configurator defers the underlying registrations until the full chain has executed, and the Npgsql outbox store is applied only as a default – a custom store selected via `UseOutboxStore` is always preserved
- The relay's locking fetch SQL is composed from the model's mapped identifiers, so custom outbox table or column names (a naming convention such as `UseSnakeCaseNamingConvention`, `ToTable`, or `HasColumnName`) are supported, and the relay dispatches strictly in `(OccurredOnUtc, Id)` order
- Retrying execution strategies are fully supported – the outbox processor runs its transactional unit inside the context's execution strategy (`Database.CreateExecutionStrategy().ExecuteAsync`), so there is no need to force `DisableRetry`

## Usage

### Minimal registration

```csharp
builder.AddDbContext<AppDbContext>(config => config
    .UseNpgsql("Default")
    .EnableOutboxProcessing());
```

### With Npgsql settings

```csharp
builder.AddDbContext<AppDbContext>(config => config
    .UseNpgsql("Default", settings =>
    {
        settings.CommandTimeout = 30;
        // A retrying execution strategy is fine; the outbox runs inside the execution strategy.
    })
    .EnableOutboxProcessing(o =>
    {
        o.BatchSize = 50;
        o.MaxRetries = 5;
    }));
```

### Applying migrations on startup

`Vulthil.SharedKernel.Infrastructure.Relational` (pulled in transitively) exposes `MigrateAsync`:

```csharp
var app = builder.Build();
await app.MigrateAsync<AppDbContext>();
app.Run();
```
