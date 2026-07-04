# Inbox Pattern (Idempotent Receiver)

Message delivery is **at-least-once**: a broker can redeliver a message (after a crash between processing and acknowledgement, a network partition, or a producer republish). Without protection, a redelivered message is processed twice, duplicating its side effects.

The inbox pattern makes consumers **idempotent**: it records which messages have already been processed and skips duplicates. `Vulthil.Messaging.Inbox` provides this as a consume filter, with the processed-marker written in the **same transaction** as the consumer's business changes — so on a relational store, processing is exactly-once.

This is an *idempotent receiver*, not a store-and-forward inbox: it rides on top of the transport's existing retry, fault, and dead-letter machinery rather than re-implementing them. Bounding a consumer that keeps failing is therefore **not** the guard's job — on RabbitMQ a poison delivery is retried up to the queue's `MaxRetryCount`, then a `Fault<T>` is published and the message is nacked to the dead-letter exchange. The filter deliberately ignores `IMessageContext.RetryCount` and adds no max-attempts of its own, keeping retry policy in one place (the transport).

## How It Works

1. A consume filter resolves the delivery's **idempotency key** (the message id by default).
2. It opens an `IIdempotencyStore` transaction and checks whether the key was already processed.
3. If it was, the consumer is **skipped** and the delivery is acknowledged.
4. Otherwise the consumer runs. Its own `SaveChanges` calls flush into the ambient transaction without committing.
5. The filter records the marker and commits — persisting the marker and the consumer's writes **atomically**.

If the consumer throws, the transaction is rolled back (marker included) and the message is reprocessed cleanly on redelivery. The marker is written **on commit, not on receipt**, so an interrupted delivery never leaves a marker that would suppress reprocessing.

The check (step 2) and the marker write (step 5) are serialized only at the **marker insert**, not across the whole unit. Two duplicates of the same key in flight *at the same time* — delivered to two consumers, or across two channels — can both pass the step-2 check and run the consumer body before either commits; the unique marker then lets only one commit, while the other loses the insert race. So deduplication is exactly-once for *sequential* redelivery, but **concurrent** duplicates can each execute the handler body once. Keep side effects idempotent if concurrent duplicate delivery is possible.

How the loser is settled depends on who owns the transaction. When the relational store opens its own transaction (no outer filter already started one), it rolls back, rechecks the marker, and returns `false` — the delivery is settled as a duplicate inline, without an exception. When the store instead joins a transaction an outer filter already opened (see [Filter Registration Order](#filter-registration-order)), it cannot commit or roll back a transaction it doesn't own, so the marker conflict is **rethrown**. The transaction's owner rolls back — discarding this delivery's business writes along with the marker — and the message is redelivered; the step-2 check then deduplicates it. Either way no duplicate side effect is ever committed, but the ambient case surfaces as an exception and a redelivery round-trip rather than a quiet skip.

## Guarantees

The guarantee depends on the store implementation:

| Store | Guarantee |
|---|---|
| Relational (`Vulthil.Messaging.Inbox.Relational`) | **Transactional exactly-once** — the marker and the consumer's writes commit in one transaction. |
| Cosmos (`Vulthil.Messaging.Inbox.Cosmos`) | **Effectively-once** — best-effort deduplication layered over idempotent-by-design writes. Cosmos has no cross-partition atomicity to rely on. |

Both EF Core stores share their entity (`InboxMessage`) and context interface (`ISaveInboxMessages`) via the base
package `Vulthil.Messaging.Inbox.EntityFrameworkCore`, so you implement `ISaveInboxMessages` once regardless of provider.

## The Idempotency Key Contract

Deduplication is only as good as the key. The key must be **stable across redeliveries of the same logical message**.

The default key is `IMessageContext.MessageId`. The Vulthil publisher assigns a fresh message id on every `PublishAsync`/`SendAsync` call, so a message-id key deduplicates **broker redelivery** of the same message — but **not** a producer that *republishes* the same logical message (which gets a new id each time). To deduplicate across republishes, supply a key selector that returns a stable business identifier:

```csharp
messaging.AddIdempotentInbox<OrderPlaced>(context => context.Message.OrderId.ToString());
```

(The transactional bus outbox in `Vulthil.Messaging.Outbox` carries a **stable** message id from capture through relay, so a relay retry of the same logical message is deduplicated here — closing the loop end-to-end. See the [outbox pattern](outbox-pattern.md).)

## Configuration

### Opt a message type in

Idempotency is opt-in per message type — only guard messages that carry a stable key:

```csharp
builder.AddMessaging(messaging =>
{
    messaging.UseRabbitMq(connectionStringKey);

    messaging.ConfigureQueue("orders", queue => queue.AddConsumer<OrderPlacedConsumer>());

    messaging.AddIdempotentInbox<OrderPlaced>();                              // key on MessageId
    messaging.AddIdempotentInbox<OrderShipped>(c => c.Message.OrderId.ToString()); // key on a business field
});
```

A registered `IIdempotencyStore` is required at consume time. Reference `Vulthil.Messaging.Inbox.Relational` for the relational implementation, or implement `IIdempotencyStore` yourself.

### Messages without a key

By default a delivery with no resolvable key is rejected with `MissingIdempotencyKeyException`, so it cannot silently bypass the guard. To process such messages without deduplication instead, set it on the inbox store registration:

```csharp
builder.Services.AddRelationalInbox<AppDbContext>(o => o.RejectMessagesWithoutKey = false);
```

### Retention

Markers accumulate — one per processed message — so prune them with an opt-in retention sweep that deletes markers older than a window. Enable it on the inbox store registration:

```csharp
builder.Services.AddRelationalInbox<AppDbContext>(o =>
{
    o.Retention.Enabled = true;                       // turn the sweep on
    o.Retention.RetentionPeriod = TimeSpan.FromDays(7);
});
```

The sweep is registered only when `Retention.Enabled` is set. Choose `RetentionPeriod` comfortably longer than the broker's maximum redelivery delay — a marker removed while a duplicate could still arrive would let that duplicate through. The sweep runs through the registered `IIdempotencyStore` when it implements `IInboxRetentionStore` (the relational and Cosmos EF Core stores do); the relational store deletes set-based with `ExecuteDelete`.

### Metrics

The guard emits metrics on a `Meter` named `"Vulthil.Messaging.Inbox"` (`InboxTelemetry.MeterName`): the counters `vulthil.inbox.processed`, `vulthil.inbox.duplicate_skipped`, and `vulthil.inbox.missing_key`. `AddRelationalInbox`/`AddCosmosInbox` auto-register the meter when `EnableMetrics` is on (the default); for a hand-built `MeterProviderBuilder`, use `metrics.AddVulthilInboxInstrumentation()`.

### Relational store

Expose the inbox set on your `DbContext`, apply the entity configuration, and register the store:

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), ISaveInboxMessages
{
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyRelationalInbox();
}
```

```csharp
builder.Services.AddRelationalInbox<AppDbContext>();
```

The consumer and the store must share the same scoped `DbContext` instance (the default with `AddDbContext`), so the consumer's writes and the marker enlist in the same transaction. The consumer keeps calling `SaveChanges` as usual — the store owns the transaction, not `SaveChanges`. Add an EF Core migration for the `InboxMessage` table as you would for any entity.

### Filter Registration Order

`IConsumeFilter<TMessage>` instances compose in plain DI registration order — the first one resolved becomes the outermost — and nothing enforces a particular order between the inbox and other transactional filters, such as `Vulthil.Messaging.Outbox`'s transactional consumer filter. Register the idempotency filter **before** (outside) a transactional consumer filter:

```csharp
messaging.AddIdempotentInbox<OrderPlaced>(context => context.Message.OrderId.ToString());
messaging.AddTransactionalConsumer<OrderPlaced>();
```

With this order the inbox filter runs first and opens its own transaction (the relational store's `ProcessAsync` sees no ambient transaction yet), and the transactional consumer filter joins it. On a concurrent duplicate the loser rolls back and rechecks inline — the efficient path described in [How It Works](#how-it-works).

Registering them in the opposite order still produces correct results — the relational store's ambient-conflict rethrow (above) guarantees a duplicate is never committed either way — but the transactional consumer filter becomes the transaction owner, so the inbox store always runs its ambient path. A concurrent duplicate then always surfaces as an exception and a redelivery, instead of the cheaper inline rollback-and-recheck. Prefer the order above.

### Cosmos store

The Cosmos store is wired the same way — apply the Cosmos mapping with `ApplyCosmosInbox()` and call `AddCosmosInbox<AppDbContext>()`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
    => modelBuilder.ApplyCosmosInbox();
```

```csharp
builder.Services.AddCosmosInbox<AppDbContext>();
```

The marker is a self-contained document keyed and partitioned by `MessageId` in its own container, so a duplicate insert conflicts and is treated as already-processed. Because Cosmos cannot commit the marker and the business write atomically, the store writes the marker **after** the consumer's own commit and the guarantee is **effectively-once** — keep your consumer's writes idempotent (deterministic ids / upserts) so a redelivery that races ahead of the marker is harmless.

## Typical Flow

```
Broker delivers message (possibly a duplicate)
    ↓
IdempotentConsumeFilter resolves the key
    ↓
IIdempotencyStore.ProcessAsync  →  owns the unit (inside the context's execution strategy)
    ↓
already processed? ── yes ──► skip consumer, ack
    │ no
    ↓
consumer runs (writes flush into the transaction)
    ↓
marker recorded + transaction committed  (atomic)
```

The filter hands the consumer invocation to `IIdempotencyStore.ProcessAsync`, which owns the whole unit. The relational store runs it inside `Database.CreateExecutionStrategy().ExecuteAsync`, so it works **whether or not EF Core retries are enabled** — there is no need to disable retries (e.g. `DisableRetry`). Under a retrying execution strategy a transient fault re-runs the unit — consumer included — on a cleared change tracker, which is consistent with at-least-once redelivery and the requirement that consumers be idempotent.

## Relationship to the Outbox

The [outbox](outbox-pattern.md) protects the **producer** side (write-atomicity of an event with the business change); the inbox protects the **consumer** side (duplicate-delivery). They are complementary: a producer-side outbox publishing at-least-once with a stable message id, plus a consumer-side inbox keyed on that id, gives end-to-end effectively-once delivery.

## When to Use

- Consumers whose side effects are not naturally idempotent (creating records, sending notifications, charging payments).
- Any consumer where reprocessing a redelivered message would be incorrect.

## Limitations

- The marker is keyed by message key alone. Two *distinct* consumers of the same message type share one marker; if each must process independently, use distinct keys (a future enhancement may scope markers per consumer).
- Cosmos and other stores without cross-partition transactions provide effectively-once, not transactional exactly-once.
- Only the marker insert is serialized, not the whole check-process-commit unit, so **concurrent** duplicates of one key can each run the consumer body once (see [How It Works](#how-it-works)). Sequential redelivery is deduplicated as expected.
