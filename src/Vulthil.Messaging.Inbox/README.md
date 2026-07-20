# Vulthil.Messaging.Inbox

[![NuGet](https://img.shields.io/nuget/v/Vulthil.Messaging.Inbox)](https://www.nuget.org/packages/Vulthil.Messaging.Inbox)

Idempotent-receiver (inbox) consume filter for [Vulthil.Messaging](https://www.nuget.org/packages/Vulthil.Messaging).
Broker delivery is at-least-once; this package deduplicates redeliveries by recording which messages have been
handled and skipping the ones already seen. With a **transactional store** the marker commits in the **same
transaction** as the consumer's business writes, giving **exactly-once processing**; without one (e.g. Cosmos)
the guarantee is **effectively-once** — see [Guarantees](#guarantees) below.

This package is persistence-agnostic: it defines the `IIdempotencyStore` contract and the consume filter. Provide
a store via `Vulthil.Messaging.Inbox.Relational` (relational reference implementation) or your own.

## Usage

Opt a message type into idempotent processing:

```csharp
builder.AddMessaging(messaging =>
{
    messaging.UseRabbitMq(/* ... */);

    messaging.ConfigureQueue("orders", queue => queue.AddConsumer<OrderPlacedConsumer>());

    // Guard OrderPlaced deliveries; dedupes on MessageId by default.
    messaging.AddIdempotentInbox<OrderPlaced>();

    // Or key off a stable business field:
    messaging.AddIdempotentInbox<OrderPlaced>(ctx => ctx.Message.OrderId.ToString());
});
```

Deliveries with no resolvable key throw `MissingIdempotencyKeyException` by default. To process them without
deduplication instead, set it on the store registration: `services.AddRelationalInbox<AppDbContext>(o => o.RejectMessagesWithoutKey = false)`.

## Guarantees

- **Relational store**: transactional exactly-once — the marker and the consumer's writes commit together.
- **Cosmos / non-transactional stores**: effectively-once (best-effort dedup over idempotent writes); see the
  documentation for details.

## Scope

This package only **deduplicates**. Bounding a persistently-failing consumer is the transport's job: on RabbitMQ
a failing delivery is retried per the consumer's retry policy (its registration's, falling back to the queue
default), then a `Fault<T>` is published and the delivery is dead-lettered when a dead-letter queue is
configured — the guard ignores `RetryCount` and adds no max-attempts of its own. It also
serializes only the marker *insert*, so two duplicates processed **concurrently** can each run the consumer body
once. Keep side effects idempotent if concurrent duplicate delivery is possible.

See the Inbox Pattern article on the [documentation site](https://vulthil.github.io/Vulthil.SharedKernel/)
for the full design and the message-id stability contract.
