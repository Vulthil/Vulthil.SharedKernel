# Vulthil.SharedKernel.Infrastructure

Use `Vulthil.SharedKernel.Infrastructure` for persistence and outbox integration. It hosts the outbox engine
(factored into [`Vulthil.SharedKernel.Outbox`](vulthil-sharedkernel-outbox.md)) and adds the `DbContext` base and
the DI wiring (`EnableOutboxProcessing`).

## When to use

- EF Core `DbContext` setup and migrations/ensure-created helpers
- Transaction wrappers and repository implementations
- Outbox persistence and background processing

## Pattern

- Keep persistence mapping in infrastructure only
- Publish domain events through outbox for reliability
- Register infrastructure via composition-root extension methods

## Usage

### Defining a DbContext

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : BaseDbContext(options)
{
    protected override Assembly? ConfigurationAssembly => typeof(AppDbContext).Assembly;

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyNpgsqlOutbox();
    }
}
```

### Registration with outbox processing

`AddDbContext` is an extension on `IHostApplicationBuilder`. The provider extension (e.g. `UseNpgsql`, in `Vulthil.SharedKernel.Infrastructure.Npgsql`) handles the EF Core registration, so chain it directly on the configurator:

```csharp
builder.AddDbContext<AppDbContext>(config => config
    .UseNpgsql("Default")
    .EnableOutboxProcessing(o =>
    {
        o.BatchSize = 10;
        o.MaxRetries = 3;
    }));
```

`UseOutboxStore<TStore>()` selects a custom `IOutboxStore`. The provider extensions propose theirs through
`UseDefaultOutboxStore<TStore>()`, which only applies when nothing was selected, so the explicit selection wins in
any order. The full outbox-plus-inbox wiring is in [Transactional Messaging](../transactional-messaging.md).

Only one outbox-enabled `DbContext` is supported per host: the relay and retention background services resolve a
single `IOutboxStore`, so a second `EnableOutboxProcessing()` call (on a different `DbContext`) throws an
`InvalidOperationException` at startup instead of silently leaving the first context's messages unrelayed.

### Several DbContexts

Each context registered with `AddDbContext` is part of the host's `IUnitOfWork`. With one context, `IUnitOfWork` is
that context. With several, `IUnitOfWork` spans all of them, so transactional commands and transactional consumers
open a transaction on every context:

- `SaveChangesAsync` saves every context, and the contexts commit one by one: in registration order, with the
  outbox-enabled context last. A failed commit can leave a saved change without its message, but never a message for
  a change that was not saved.
- A commit is atomic per context only. When a commit fails after another context committed, the committed changes
  stay saved, the rest is rolled back, and `ExecuteInTransactionAsync` throws an `InvalidOperationException` instead
  of retrying, because a retry would repeat the committed changes.
- Retries follow the execution strategy of the first registered context. With two kinds of database, a transient
  fault of the second kind is not retried.
- A context whose provider has no transactions (for example Cosmos DB) stays out of the transaction: its saves are
  not rolled back with the others.
- Every transactional unit opens a transaction, and a connection, on every context, even when it uses only one.

To make one context the unit of work instead, register `IUnitOfWork` yourself after the `AddDbContext` calls, for
example `services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<OrdersDbContext>())`.

### Database initialization

```csharp
// Apply pending migrations on startup
await app.MigrateAsync<AppDbContext>();

// Or ensure created (for development/testing)
await app.EnsureCreatedAsync<AppDbContext>();
```
