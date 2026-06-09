# Vulthil.Messaging.Outbox

A transactional bus-publish outbox for `Vulthil.Messaging`. Messages published or sent while a database transaction
is open are captured into the shared outbox table (atomically with the business changes) and relayed to the broker
**after** the transaction commits — eliminating the dual-write problem. A stable message id is carried through, so a
redelivered relay is deduplicated by the receiving inbox (end-to-end effectively-once).

## When to use

- A consumer or command writes to the database **and** publishes an integration event, and the publish must happen
  if (and only if) the business change commits.

## Pattern

- Reuses the shared outbox engine — one `OutboxMessages` table and one relay serve both in-process domain events
  and broker messages, routed by an `OutboxDestination` discriminator.
- Capture is gated on an ambient transaction: a publish/send with no active transaction is sent directly.
- A commit-time trigger relays freshly-committed messages promptly; the periodic poll is the backstop.
- Built on the general publish/send filter pipeline (`IPublishFilter` / `AddPublishFilter<T>()`).

## Usage

```csharp
builder.AddDbContext<AppDbContext>(config => config
    .UseNpgsql("Default")
    .EnableOutboxProcessing());

builder.AddMessaging(messaging =>
{
    messaging.UseRabbitMq();
    messaging.AddTransactionalOutbox();
});
```

The application's `DbContext` must implement `ISaveOutboxMessages` (a `BaseDbContext` already does). Capture is
relational-only (it enlists in the ambient transaction); the relay works on any provider.

See the [Outbox Pattern](https://github.com/Vulthil/Vulthil.SharedKernel/tree/main/docs/articles/outbox-pattern.md)
article for the design, the pluggable-sink model, and the commit-time trigger.
