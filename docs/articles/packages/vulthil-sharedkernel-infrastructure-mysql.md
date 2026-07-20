# Vulthil.SharedKernel.Infrastructure.MySql

Use `Vulthil.SharedKernel.Infrastructure.MySql` to wire a MySQL-backed `DbContext` together with the MySQL-tuned outbox store.

## When to use

- MySQL is the underlying database for an application's primary `DbContext`
- Outbox processing should use the MySQL store (row-level locking via `FOR UPDATE SKIP LOCKED`)

## Pattern

- Call `UseMySql("ConnectionStringKey")` on the database infrastructure configurator – it registers a pooled EF Core context for the named connection string, selects the MySQL outbox store, and (when outbox processing is enabled) wires the commit-time relay trigger
- Order between `UseMySql`, `EnableOutboxProcessing`, and `UseOutboxStore` does not matter; the configurator defers the underlying registrations until the full chain has executed, and the MySQL outbox store is applied only as a default – a custom store selected via `UseOutboxStore` is always preserved
- The relay's locking fetch SQL is composed from the model's mapped identifiers, so custom outbox table or column names (a naming convention, `ToTable`, or `HasColumnName`) are supported, and the relay dispatches strictly in `(OccurredOnUtc, Id)` order
- Retrying execution strategies are fully supported – the outbox processor runs its transactional unit inside the context's execution strategy (`Database.CreateExecutionStrategy().ExecuteAsync`)

## Provider

The MySQL EF Core provider is resolved per target framework:

- **.NET 9** – the Aspire client integration [`Aspire.Pomelo.EntityFrameworkCore.MySql`](https://www.nuget.org/packages/Aspire.Pomelo.EntityFrameworkCore.MySql), which resolves the connection string and adds health checks, telemetry, and connection resiliency (parity with the Npgsql and Cosmos integrations)
- **.NET 10** – the API-compatible [`Microting.EntityFrameworkCore.MySql`](https://www.nuget.org/packages/Microting.EntityFrameworkCore.MySql) fork of Pomelo, registered directly, until the official Pomelo and Aspire packages ship EF Core 10 support

On .NET 10 the MySQL server version is detected from the connection at startup via `ServerVersion.AutoDetect`, so the database must be reachable when the host starts.

## Usage

### Registration

```csharp
builder.AddDbContext<AppDbContext>(config => config
    .UseMySql("Default")
    .EnableOutboxProcessing(o =>
    {
        o.BatchSize = 25;
        o.MaxRetries = 3;
    }));
```

### Configuring the integration

`UseMySql` takes an optional `configureSettings` delegate. Because the Aspire integration only exists on .NET 9, its type differs per target framework — the Aspire `PomeloEntityFrameworkCoreMySqlSettings` on .NET 9, and the EF Core `MySqlDbContextOptionsBuilder` on .NET 10:

```csharp
// .NET 9 (Aspire) — toggle health checks, tracing, retries, command timeout:
config.UseMySql("Default", settings => settings.DisableHealthChecks = true);

// .NET 10 (Microting) — configure EF Core/Pomelo options:
config.UseMySql("Default", mySql => mySql.CommandTimeout(30));
```

### Applying migrations on startup

`Vulthil.SharedKernel.Infrastructure.Relational` (pulled in transitively) exposes `MigrateAsync`:

```csharp
var app = builder.Build();
await app.MigrateAsync<AppDbContext>();
app.Run();
```
