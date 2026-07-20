# Vulthil.SharedKernel.Infrastructure.Relational

Use `Vulthil.SharedKernel.Infrastructure.Relational` as the shared base for relational EF Core providers: it holds the relational outbox store base class, the commit-time relay trigger, and the `MigrateAsync` startup helper.

## When to use

- Building a provider-specific package (Npgsql, MySql, SqlServer, …) that should reuse a common outbox implementation
- Applying pending EF Core migrations from application startup

## Pattern

- Pull this package in transitively via the provider package (`Vulthil.SharedKernel.Infrastructure.Npgsql`, `…MySql`, etc.); it is rarely referenced directly from application code
- Derive provider-specific outbox stores from `RelationalOutboxStore<TContext>` to add row-level locking or other provider-specific tuning
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

If you are authoring a new provider, inherit from `RelationalOutboxStore<TContext>` and override the fetch to add provider-specific row locking:

```csharp
public sealed class MyProviderOutboxStore<TContext>(
    TContext dbContext, TimeProvider timeProvider, IOptions<OutboxProcessingOptions> options)
    : RelationalOutboxStore<TContext>(dbContext, timeProvider, options)
    where TContext : DbContext, ISaveOutboxMessages
{
    protected override Task<List<OutboxMessageData>> FetchMessagesAsync(
        int batchSize, int maxRetries, CancellationToken cancellationToken)
    {
        // Add provider-specific FOR UPDATE SKIP LOCKED or equivalent here.
        return base.FetchMessagesAsync(batchSize, maxRetries, cancellationToken);
    }
}
```

### Commit-time relay trigger

`AddRelationalOutboxCommitTrigger()` registers a transaction interceptor (`OutboxCommitInterceptor`) that wakes the outbox relay when an explicit database transaction commits, so captured messages relay promptly instead of waiting for the next poll. Provider `Use*` extensions register it automatically when outbox processing is enabled.
