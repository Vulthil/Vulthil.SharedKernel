# Vulthil.SharedKernel.Infrastructure.MySql

Use `Vulthil.SharedKernel.Infrastructure.MySql` to wire a MySQL-backed `DbContext` together with the MySQL-tuned outbox store.

## When to use

- MySQL is the underlying database for an application's primary `DbContext`
- Outbox processing should use the MySQL store (row-level locking via `FOR UPDATE SKIP LOCKED`)

## Pattern

- Call `UseMySql("ConnectionStringKey")` on the database infrastructure configurator – it registers a pooled EF Core context for the named connection string, selects the MySQL outbox store, and (when outbox processing is enabled) wires the commit-time relay trigger
- Call `modelBuilder.ApplyMySqlOutbox()` in `OnModelCreating` for the MySQL-tuned outbox mapping: `longtext` content, a composite pending-message index (MySQL has no filtered indexes), and a `Type` column with no length limit, because it stores the message type's full name, which includes the full assembly name of every generic type argument
- Order between `UseMySql`, `EnableOutboxProcessing`, and `UseOutboxStore` does not matter; the configurator defers the underlying registrations until the full chain has executed, and the MySQL outbox store is applied only as a default – a custom store selected via `UseOutboxStore` is always preserved
- The relay's locking fetch SQL is composed from the model's mapped identifiers, so custom outbox table or column names (a naming convention, `ToTable`, or `HasColumnName`) are supported, and the relay dispatches strictly in `(OccurredOnUtc, Id)` order
- Retrying execution strategies are fully supported – the outbox processor runs its transactional unit inside the context's execution strategy (`Database.CreateExecutionStrategy().ExecuteAsync`)

## Provider

The MySQL EF Core provider is [`Microting.EntityFrameworkCore.MySql`](https://www.nuget.org/packages/Microting.EntityFrameworkCore.MySql), registered directly.

The MySQL server version is detected from the connection at startup via `ServerVersion.AutoDetect`, so the database must be reachable when the host starts.

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

### Configuring the provider

`UseMySql` takes two optional delegates. `configureMySql` configures the MySQL provider options
(`MySqlDbContextOptionsBuilder`). `configureDbContextOptions` configures the `DbContextOptionsBuilder`, for example to
add interceptors or a naming convention; it runs after the MySQL provider is selected:

```csharp
config.UseMySql(
    "Default",
    configureMySql: mySql => mySql.CommandTimeout(30),
    configureDbContextOptions: options => options.AddInterceptors(new AuditInterceptor()));
```

### Applying migrations on startup

`Vulthil.SharedKernel.Infrastructure.Relational` (pulled in transitively) exposes `MigrateAsync`:

```csharp
var app = builder.Build();
await app.MigrateAsync<AppDbContext>();
app.Run();
```
