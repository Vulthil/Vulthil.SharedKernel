# Vulthil.SharedKernel.Outbox

The transactional **outbox engine** — the message-capture model, the relay processor and background service, the
pluggable sinks (`IOutboxDispatcher`), the commit-time relay signal/gate, and the provider-agnostic strategy
contracts (`IOutboxStrategy` / `BaseOutboxStrategy`).

## When to use

- You normally consume the outbox transitively. Reference `Vulthil.SharedKernel.Infrastructure` and call
  `EnableOutboxProcessing()` — it hosts this engine and adds the `DbContext` base + DI wiring.
- Reference this package **directly** only when you need the engine contracts without the rest of the infrastructure
  package — for example a messaging bridge such as `Vulthil.Messaging.Outbox`, which captures and relays broker
  messages and depends on the engine alone.

## Pattern

- One `OutboxMessages` table, one relay; rows are routed by an `OutboxDestination` discriminator to the registered
  `IOutboxDispatcher`, so in-process domain events and broker messages share a single outbox.
- The engine types live in the `Vulthil.SharedKernel.Outbox` namespace. The `OutboxMessages` table shape is
  unchanged, so existing EF migrations continue to apply.

See the [Outbox Pattern](https://github.com/Vulthil/Vulthil.SharedKernel/tree/main/docs/articles/outbox-pattern.md)
article for the design, the pluggable-sink model, and the commit-time trigger.
