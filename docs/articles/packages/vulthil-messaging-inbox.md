# Vulthil.Messaging.Inbox

An idempotent-receiver (inbox) consume filter for `Vulthil.Messaging`. Broker delivery is at-least-once; this
package deduplicates redeliveries by recording which messages were handled and skipping the ones already seen.
With a **transactional store** the marker commits in the same transaction as the consumer's business writes,
giving **exactly-once processing**; without one (e.g. Cosmos) the guarantee is **effectively-once**.

Persistence-agnostic: it defines the `IIdempotencyStore` contract and the filter. Provide a store via
`Vulthil.Messaging.Inbox.Relational` (relational reference implementation) or your own.

## When to use

- Consumers whose side effects are not naturally idempotent (creating records, sending notifications, charging)
- Any consumer where reprocessing a redelivered message would be incorrect

## Pattern

- Opt-in per message type with `AddIdempotentInbox<TMessage>()`
- Dedupes on `MessageId` by default; pass a key selector to dedupe on a stable business field
- The store owns the transactional unit (the filter hands it the consumer invocation); the consumer keeps calling `SaveChanges` as usual
- Deliveries with no resolvable key are rejected (`MissingIdempotencyKeyException`) unless you opt out
- Prune markers with `AddInboxRetention(...)` — an opt-in background sweep that deletes markers older than a window

## Usage

```csharp
builder.AddMessaging(messaging =>
{
    messaging.UseRabbitMq(connectionStringKey);
    messaging.ConfigureQueue("orders", queue => queue.AddConsumer<OrderPlacedConsumer>());

    messaging.AddIdempotentInbox<OrderPlaced>();                                   // key on MessageId
    messaging.AddIdempotentInbox<OrderShipped>(ctx => ctx.Message.OrderId.ToString()); // key on a business field
});
```

See the [Inbox Pattern](https://github.com/Vulthil/Vulthil.SharedKernel/tree/main/docs/articles/inbox-pattern.md)
article for the design, guarantees, and the message-id stability contract.
