# Outbox Pattern

`Vulthil.SharedKernel.Infrastructure` implements the transactional outbox pattern so that domain events raised by aggregate roots are guaranteed to be published even if the process crashes after the database commit.

## How It Works

1. During `SaveChangesAsync`, a `SaveChangesInterceptor` serialises every pending domain event into an `OutboxMessage` row in the same database transaction.
2. The aggregate root's event collection is cleared.
3. A background service (`OutboxBackgroundService`) periodically polls for unprocessed outbox messages.
4. Each message is deserialised and dispatched through `IDomainEventPublisher`.
5. Successfully published messages are marked as processed; failures are retried up to the configured maximum.

This guarantees **at-least-once delivery** because the event and the business data are committed atomically.

## Configuration

### Enable outbox processing during DbContext registration

`AddDbContext` is an extension on `IHostApplicationBuilder` (not `IServiceCollection`). Provider extensions such as `UseNpgsql` are called directly on the configurator — they register the EF Core context for you, so no separate `AddDbContext`/options callback is needed.

```csharp
builder.AddDbContext<AppDbContext>(config =>
{
    config
        .UseNpgsql(connectionStringKey)
        .EnableOutboxProcessing(o =>
        {
            o.BatchSize = 10;   // Messages fetched per poll cycle
            o.MaxRetries = 3;   // Retry limit before a message is abandoned
        });
});
```

### DbContext requirements

Your context must derive from `BaseDbContext`, which already implements `ISaveOutboxMessages` and includes the `OutboxMessages` `DbSet`:

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : BaseDbContext(options)
{
    protected override Assembly? ConfigurationAssembly =>
        typeof(AppDbContext).Assembly;

    public DbSet<User> Users => Set<User>();
}
```

The `OutboxMessage` entity configuration is applied automatically by `BaseDbContext.OnModelCreating`.

## Outbox Processing Options

| Property | Default | Description |
|---|---|---|
| `BatchSize` | 10 | Number of messages fetched per poll cycle |
| `MaxRetries` | 3 | Maximum publish attempts before a message is abandoned |
| `EnableParallelPublishing` | `false` | Publish messages in parallel within a batch |
| `OutboxProcessingDelayInSeconds` | 2 | Base polling delay between processing cycles |
| `MaxDelaySeconds` | 60 | Maximum back-off delay when no messages are found |
| `EnableTracing` | `true` | Carry the originating trace identifier when publishing |

## Custom Outbox Strategy

The default strategy is provided by `Vulthil.SharedKernel.Infrastructure.Relational` (`RelationalOutboxStrategy`) and runs a relational query with row locking. You can replace it by implementing `IOutboxStrategy` and registering your implementation with `UseOutboxStrategy<T>()`:

```csharp
config
    .UseNpgsql(connectionStringKey)
    .UseOutboxStrategy<CustomOutboxStrategy>()
    .EnableOutboxProcessing(o =>
    {
        o.BatchSize = 50;
    });
```

## Typical Flow

```
Aggregate.Raise(event)
    ↓
SaveChangesAsync  →  OutboxMessage row inserted (same transaction)
    ↓
OutboxBackgroundService polls
    ↓
OutboxProcessor deserialises & publishes via IDomainEventPublisher
    ↓
Message marked as processed (or retried on failure)
```

## When to Use

- You need reliable event delivery across service boundaries.
- You want to avoid dual-write problems (writing to the database and a message broker in separate transactions).
- Your domain events trigger side-effects in other bounded contexts or external systems.
