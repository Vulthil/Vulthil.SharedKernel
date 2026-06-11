# Outbox Pattern

The transactional outbox pattern guarantees that domain events raised by aggregate roots are published even if the process crashes after the database commit. The engine lives in `Vulthil.SharedKernel.Outbox`; `Vulthil.SharedKernel.Infrastructure` references it and adds the `DbContext` base and the `EnableOutboxProcessing` wiring.

## How It Works

1. During `SaveChangesAsync`, a `SaveChangesInterceptor` serialises every pending domain event into an `OutboxMessage` row in the same database transaction.
2. The aggregate root's event collection is cleared.
3. A background service (`OutboxBackgroundService`) relays unprocessed outbox messages — woken immediately once the captured rows are durable (on transaction commit, or right after a non-transactional `SaveChanges` that captured domain events) for low latency, and polling on an interval as the backstop.
4. Each message is routed by its `OutboxDestination` to the registered `IOutboxDispatcher` that handles it (in-process domain events by default, or the broker — see below).
5. Successfully relayed messages are marked as processed; failures are retried up to the configured maximum.

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

## Custom Outbox Store

The relay engine talks to the database through an EF-free `IOutboxStore` (in `Vulthil.SharedKernel.Outbox`). The EF
implementation lives in `Vulthil.SharedKernel.Outbox.EntityFrameworkCore` (`EntityFrameworkOutboxStore<TContext>`),
and each provider supplies a subclass with its row-locking fetch — `RelationalOutboxStore<TContext>` (the
`ExecuteUpdate` base), `NpgsqlOutboxStore<TContext>` / `MySqlOutboxStore<TContext>` (`FOR UPDATE SKIP LOCKED`), and
`CosmosOutboxStore<TContext>` (best-effort, no transaction). A provider's `UseNpgsql`/`UseMySql`/`UseCosmosDb`
selects the store; you can supply your own by implementing `IOutboxStore` (or deriving from the EF base) and
registering it with `UseOutboxStore<T>()`:

```csharp
config
    .UseNpgsql(connectionStringKey)
    .UseOutboxStore<CustomOutboxStore<AppDbContext>>()
    .EnableOutboxProcessing(o =>
    {
        o.BatchSize = 50;
    });
```

## One outbox, multiple sinks

The relay engine is sink-agnostic: each `OutboxMessage` carries an `OutboxDestination` discriminator, and the
`OutboxProcessor` routes it to the registered `IOutboxDispatcher` whose `Handles(destination)` is true. The
in-process domain-event dispatcher is registered by default; other sinks plug in and coexist in the **same** outbox
table and relay, so an application never carries more than one outbox table regardless of how many sinks it uses.

## Transactional bus-publish outbox

`Vulthil.Messaging.Outbox` adds a sink for the message broker. A publish/send filter captures any message published
while a database transaction is open into the same outbox table (atomically with the business changes); the relay
sends it to the broker after the transaction commits, carrying a **stable message id** so a redelivered relay is
deduplicated by the receiving [inbox](inbox-pattern.md) — end-to-end effectively-once. A publish issued with no
active transaction is sent directly.

```csharp
builder.AddDbContext<AppDbContext>(config => config.UseNpgsql("Default").EnableOutboxProcessing());

builder.AddMessaging(messaging =>
{
    messaging.UseRabbitMq();
    messaging.AddTransactionalOutbox();
});
```

Capture is relational-only (it enlists in the ambient transaction); the relay works on any provider. It is built on
the general publish/send **filter pipeline** (`IPublishFilter`, registered via `AddPublishFilter<T>()`), which is the
publish-side counterpart to consume filters and can host other cross-cutting concerns.

On startup the relay waits for the broker transport to finish declaring its subscriber topology (exchanges, queues,
and bindings) before its first publish — otherwise the commit-time trigger could relay a message before the
subscriber queues exist, and a pub/sub message with no bound queue is silently dropped. This is wired automatically
by `AddTransactionalOutbox` via an `IOutboxRelayGate` that awaits `ITransport.WaitUntilReadyAsync`; the relay starts
immediately when no gate is registered.

### Establishing the transaction

Capture only happens when a database transaction is open around the publish — otherwise the message is sent
directly. The transaction is established by one of:

- **Commands** — mark them `ITransactionalCommand<T>` and register `AddTransactionalPipelineBehavior()`; the
  behavior runs the command in a transaction.
- **Consumers** — the [inbox](inbox-pattern.md) opens one, or call `messaging.AddTransactionalConsumer<TMessage>()`
  to run a consumer in a transaction without the inbox. The two compose: if the inbox is also enabled it opens the
  transaction and the consume filter joins it rather than nesting.
- **Anything else** — wrap the work in `IUnitOfWork.ExecuteInTransactionAsync(...)`.

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
