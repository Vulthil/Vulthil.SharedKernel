# Outbox Pattern

The transactional outbox pattern guarantees that domain events raised by aggregate roots are published even if the process crashes after the database commit. The engine lives in `Vulthil.SharedKernel.Outbox`; `Vulthil.SharedKernel.Infrastructure` references it and adds the `DbContext` base and the `EnableOutboxProcessing` wiring.

## How It Works

1. During `SaveChangesAsync`, a `SaveChangesInterceptor` serialises every pending domain event into an `OutboxMessage` row in the same database transaction.
2. The aggregate root's event collection is cleared.
3. A background service (`OutboxBackgroundService`) relays unprocessed outbox messages — woken immediately once the captured rows are durable (on transaction commit, or right after a non-transactional `SaveChanges` that captured domain events) for low latency, and polling on an interval as the backstop.
4. Each message is routed by its `OutboxDestination` to the registered `IOutboxDispatcher` that handles it (in-process domain events by default, or the broker — see below).
5. Successfully relayed messages are marked as processed; failures are retried up to the configured maximum, after which the message is dead-lettered — its `FailedOnUtc` timestamp is set, it is no longer relayed, and an error is logged with the last failure (the `Error` column).

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
            o.MaxRetries = 3;   // Retries before a message is dead-lettered
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyNpgsqlOutbox();
    }
}
```

`BaseDbContext` owns the `OutboxMessages` `DbSet`; apply the provider-optimized mapping in `OnModelCreating` by calling your provider's extension — `ApplyNpgsqlOutbox()`, `ApplyMySqlOutbox()`, or `ApplyCosmosOutbox()` (as shown above). The agnostic `ApplyOutbox()` is available for custom providers.

## Outbox Processing Options

| Property | Default | Description |
|---|---|---|
| `BatchSize` | 10 | Number of messages fetched per poll cycle |
| `MaxRetries` | 3 | Publish attempts before a message is dead-lettered (`FailedOnUtc` set, no longer relayed) |
| `EnableParallelPublishing` | `false` | Publish messages in parallel within a batch (each dispatch runs in its own DI scope) |
| `MaxDegreeOfParallelism` | 4 | Maximum concurrent dispatches when `EnableParallelPublishing` is enabled |
| `OutboxProcessingDelaySeconds` | 2 | Base polling delay between processing cycles |
| `MaxDelaySeconds` | 60 | Maximum back-off delay when no messages are found |
| `EnableTracing` | `true` | Carry the originating trace identifier when publishing |

## Observability

The relay emits an `ActivitySource` named `"Vulthil.SharedKernel.Outbox"` (exposed as `Telemetry.ActivitySourceName`). When `EnableTracing` is on (the default), each relayed message starts an `OutboxPublishing` span parented on the trace that captured the row — the originating trace is carried forward through the `OutboxMessage.TraceParent`/`TraceState` columns stamped at capture — so the relay, which runs later on its own background service, still correlates back to the request that produced the message.

`AddOutboxEngine` (called by `EnableOutboxProcessing`) registers the source with OpenTelemetry automatically when `EnableTracing` is on (the default), so the spans reach whatever tracer the application has configured without extra wiring. If you build a `TracerProviderBuilder` yourself, the same registration is available as `tracing.AddVulthilOutboxInstrumentation()` — sugar for `AddSource(Telemetry.ActivitySourceName)`.

The relay also emits **metrics** on a `Meter` named `Telemetry.MeterName` (`"Vulthil.SharedKernel.Outbox"`): the counters `vulthil.outbox.relayed` and `vulthil.outbox.failed`. `AddOutboxEngine` auto-registers the meter with OpenTelemetry when `EnableMetrics` is on (the default); for a hand-built `MeterProviderBuilder`, use `metrics.AddVulthilOutboxInstrumentation()`.

## Retention

Processed and dead-lettered rows remain in the `OutboxMessages` table after relay, so the table grows unbounded unless they are pruned. Opt into a retention sweep — a background service that periodically deletes terminal rows older than a window — by enabling `Retention` on the outbox options:

```csharp
.EnableOutboxProcessing(o =>
{
    o.Retention.Enabled = true;                          // turn the sweep on
    o.Retention.RetentionPeriod = TimeSpan.FromDays(7);  // delete processed/dead-lettered rows older than this
    o.Retention.SweepInterval = TimeSpan.FromHours(1);
    o.Retention.BatchSize = 1000;
});
```

`AddOutboxEngine` (called by `EnableOutboxProcessing`) registers the sweep only when `Retention.Enabled` is set, so it costs nothing when off.

| `Retention` property | Default | Description |
|---|---|---|
| `Enabled` | `false` | Whether the retention sweep runs |
| `RetentionPeriod` | 7 days | How long a processed or dead-lettered row is kept |
| `SweepInterval` | 1 hour | Delay between sweeps |
| `BatchSize` | 1000 | Rows deleted per batch within a sweep |

The sweep deletes rows whose `ProcessedOnUtc` **or** `FailedOnUtc` is older than `RetentionPeriod`; **pending rows are never touched**. It runs through the registered `IOutboxStore` when it implements `IOutboxRetentionStore` (the EF Core store does) — relational providers delete set-based with `ExecuteDelete`, and the **same sweep covers Cosmos** (a Cosmos container TTL is not used, because it cannot tell a pending row from a relayed one and could expire an undelivered message).

## Custom Outbox Store

The relay engine talks to the database through an EF-free `IOutboxStore` (in `Vulthil.SharedKernel.Outbox`). The EF
implementation lives in `Vulthil.SharedKernel.Outbox.EntityFrameworkCore` (`EntityFrameworkOutboxStore<TContext>`),
and each provider supplies a subclass with its row-locking fetch — `RelationalOutboxStore<TContext>` (the
`ExecuteUpdate` base), `NpgsqlOutboxStore<TContext>` / `MySqlOutboxStore<TContext>` (`FOR UPDATE SKIP LOCKED`), and
`CosmosOutboxStore<TContext>` (best-effort, no transaction). A provider's `UseNpgsql`/`UseMySql`/`UseCosmosDb`
selects the store; you can supply your own by implementing `IOutboxStore` (or deriving from the EF base) and
registering it with `UseOutboxStore<TStore>()`:

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
Message marked as processed (or retried on failure, then dead-lettered after MaxRetries)
```

## When to Use

- You need reliable event delivery across service boundaries.
- You want to avoid dual-write problems (writing to the database and a message broker in separate transactions).
- Your domain events trigger side-effects in other bounded contexts or external systems.
