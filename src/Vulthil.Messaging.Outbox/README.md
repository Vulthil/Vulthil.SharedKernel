# Vulthil.Messaging.Outbox

Transactional bus-publish outbox for `Vulthil.Messaging`. Publishes and sends issued while a database transaction
is open are captured into the shared outbox table (atomically with the business changes) and relayed to the broker
after the transaction commits — eliminating the dual-write problem. A stable message id is carried through, so a
redelivered relay is deduplicated by the receiving inbox (end-to-end effectively-once).

## When to use

- A consumer or command writes to the database **and** publishes an integration event, and the publish must not
  happen unless the business change commits (and must happen if it does).

## Pattern

- Reuses the shared outbox engine — one `OutboxMessages` table and one relay serve both in-process domain events
  and broker messages, routed by an `OutboxDestination` discriminator.
- Capture is gated on an ambient transaction: a publish/send with no active transaction is sent directly.
- A commit-time trigger relays freshly-committed messages promptly; the periodic poll is the backstop.

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
relational-only (it needs a transaction to enlist in).
