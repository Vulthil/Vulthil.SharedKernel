# Vulthil.SharedKernel.Infrastructure.Npgsql

Use `Vulthil.SharedKernel.Infrastructure.Npgsql` to wire a PostgreSQL-backed `DbContext` together with the PostgreSQL-tuned outbox strategy.

## When to use

- PostgreSQL is the underlying database for an application's primary `DbContext`
- Aspire-wired connection strings via `AddNpgsqlDbContext`
- Outbox processing should use the PostgreSQL strategy (row-level locking via `FOR UPDATE SKIP LOCKED`)

## Pattern

- Call `UseNpgsql("ConnectionStringKey")` on the database infrastructure configurator – it both registers the EF Core context and selects the Npgsql outbox strategy
- Order between `UseNpgsql` and `EnableOutboxProcessing` does not matter; the configurator defers the underlying call until the full chain has executed
- EF Core retries are left enabled – the outbox processor runs its transactional unit inside the context's execution strategy (`Database.CreateExecutionStrategy().ExecuteAsync`), which is compatible with a retrying execution strategy, so there is no need to force `DisableRetry`

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
        // EF Core retries are left enabled; the outbox runs inside the execution strategy.
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
