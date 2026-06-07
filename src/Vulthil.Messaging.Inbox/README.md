# Vulthil.Messaging.Inbox

Idempotent-receiver (inbox) consume filter for [Vulthil.Messaging](https://www.nuget.org/packages/Vulthil.Messaging).
Broker delivery is at-least-once; this package gives **exactly-once processing** by recording which messages
have been handled and skipping duplicates — with the marker committed in the **same transaction** as the
consumer's business writes.

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
deduplication instead, call `messaging.ConfigureInbox(o => o.RejectMessagesWithoutKey = false)`.

## Guarantees

- **Relational store**: transactional exactly-once — the marker and the consumer's writes commit together.
- **Cosmos / non-transactional stores**: effectively-once (best-effort dedup over idempotent writes); see the
  documentation for details.

See the [inbox pattern documentation](https://vulthil.github.io/Vulthil.SharedKernel/articles/inbox-pattern.html)
for the full design and the message-id stability contract.
