# Vulthil.SharedKernel.Infrastructure.Relational

Use `Vulthil.SharedKernel.Infrastructure.Relational` as the shared base for relational EF Core providers: it holds the relational outbox store base class, the commit-time relay trigger, and the `MigrateAsync` startup helper.

## When to use

- Building a provider-specific package (Npgsql, MySql, SqlServer, …) that should reuse a common outbox implementation
- Applying pending EF Core migrations from application startup

## Pattern

- Pull this package in transitively via the provider package (`Vulthil.SharedKernel.Infrastructure.Npgsql`, `…MySql`, etc.); it is rarely referenced directly from application code
- Derive provider-specific outbox stores from `RelationalOutboxStore<TContext>`; pass your dialect's row-locking clause to `FetchMessagesWithRowLockAsync`, or override the fetch for other provider-specific tuning
- Apply migrations during startup rather than at build time so deployments stay reproducible

## Usage

### Applying migrations on startup

```csharp
var app = builder.Build();

// IHost overload – use from Program.cs after Build()
await app.MigrateAsync<AppDbContext>();

app.MapEndpoints();
app.Run();
```

`MigrateAsync` checks for pending migrations and only invokes `Database.MigrateAsync()` when at least one is pending, so it is safe to call on every startup.

### Reusing the relational outbox store

Provider packages wire `RelationalOutboxStore<TContext>` (or a subclass of it) automatically through their own `Use*` extension. The base class records relay outcomes with set-based `ExecuteUpdate` calls, deletes retention batches by key, and requires the relay batch to run inside a transaction — it throws at relay time if `TContext` does not implement `IUnitOfWork` (derive from `BaseDbContext`), because without a transaction provider row-locking such as `FOR UPDATE SKIP LOCKED` would release immediately after the fetch and concurrent relay instances could double-dispatch.

If you are authoring a new provider, inherit from `RelationalOutboxStore<TContext>` and override the fetch. `FetchMessagesWithRowLockAsync` composes the `SELECT … ORDER BY … LIMIT` statement from the model's mapped table and column names (so renamed identifiers keep working) and appends the row-locking clause you pass — this is exactly what the Npgsql and MySQL stores do:

```csharp
public sealed class MyProviderOutboxStore<TContext>(
    TContext dbContext, TimeProvider timeProvider, IOptions<OutboxProcessingOptions> options)
    : RelationalOutboxStore<TContext>(dbContext, timeProvider, options)
    where TContext : DbContext, ISaveOutboxMessages
{
    protected override Task<List<OutboxMessageData>> FetchMessagesAsync(
        int batchSize, int maxRetries, CancellationToken cancellationToken) =>
        FetchMessagesWithRowLockAsync("FOR UPDATE SKIP LOCKED", batchSize, maxRetries, cancellationToken);
}
```

A dialect that cannot express its lock this way, or that pages with something other than `LIMIT`, overrides `FetchMessagesAsync` with its own query instead.

### Commit-time relay trigger

`AddRelationalOutboxCommitTrigger()` registers a transaction interceptor (`OutboxCommitInterceptor`) that wakes the outbox relay when an explicit database transaction commits, so captured messages relay promptly instead of waiting for the next poll. Provider `Use*` extensions register it automatically when outbox processing is enabled.
