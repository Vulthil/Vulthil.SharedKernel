# Vulthil.SharedKernel.Infrastructure.Relational

Use `Vulthil.SharedKernel.Infrastructure.Relational` as the shared base for relational EF Core providers and as the source of the default relational outbox strategy.

## When to use

- Building a provider-specific package (Npgsql, MySql, SqlServer, …) that should reuse a common outbox implementation
- Applying pending EF Core migrations from application startup

## Pattern

- Pull this package in transitively via the provider package (`Vulthil.SharedKernel.Infrastructure.Npgsql`, `…MySql`, etc.); it is rarely referenced directly from application code
- Derive provider-specific outbox strategies from `RelationalOutboxStrategy` to add row-level locking or other provider-specific tuning
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

### Reusing the relational outbox strategy

Provider packages wire `RelationalOutboxStrategy` (or a subclass of it) automatically through their own `Use*` extension. If you are authoring a new provider, inherit from `RelationalOutboxStrategy` and override `FetchMessagesAsync` to add provider-specific row locking:

```csharp
public sealed class MyProviderOutboxStrategy : RelationalOutboxStrategy
{
    public override Task<List<OutboxMessageData>> FetchMessagesAsync(
        DbSet<OutboxMessage> outboxMessages,
        int batchSize,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        // Add provider-specific FOR UPDATE SKIP LOCKED or equivalent here.
        return base.FetchMessagesAsync(outboxMessages, batchSize, maxRetries, cancellationToken);
    }
}
```
