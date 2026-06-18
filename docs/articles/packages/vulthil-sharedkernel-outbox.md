# Vulthil.SharedKernel.Outbox

The transactional **outbox engine** — the message-capture model, the relay processor and background service, the
pluggable sinks (`IOutboxDispatcher`), the commit-time relay signal/gate, and the persistence-agnostic `IOutboxStore`
seam. It has **no EF Core dependency**; the EF implementation lives in
[`Vulthil.SharedKernel.Outbox.EntityFrameworkCore`](vulthil-sharedkernel-outbox-entityframeworkcore.md).

## When to use

- You normally consume the outbox transitively. Reference `Vulthil.SharedKernel.Infrastructure` and call
  `EnableOutboxProcessing()` — it hosts this engine and adds the `DbContext` base + DI wiring.
- Reference this package **directly** only when you need the EF-free engine contracts — for example a messaging
  bridge such as `Vulthil.Messaging.Outbox`, which captures and relays broker messages through `IOutboxStore` and
  depends on the engine alone (no EF Core, no infrastructure package).

## Pattern

- One `OutboxMessages` table, one relay; rows are routed by an `OutboxDestination` discriminator to the registered
  `IOutboxDispatcher`, so in-process domain events and broker messages share a single outbox.
- The engine relies on `IOutboxStore` for both capture (`AddOutboxMessage`/`SaveChangesAsync`/`IsInTransaction`) and
  the relay batch unit (`ProcessBatchAsync`); the EF implementation and provider stores supply the transaction and
  row-locking. `AddOutboxEngine` registers the engine's own internals.
- Relay spans are emitted on the `ActivitySource` `"Vulthil.SharedKernel.Outbox"` (`Telemetry.ActivitySourceName`),
  auto-registered with OpenTelemetry by `AddOutboxEngine` (manual: `tracing.AddVulthilOutboxInstrumentation()`).
- Opt into a retention sweep via `EnableOutboxProcessing(o => o.Retention.Enabled = true)` to periodically delete processed and dead-lettered rows
  older than a window (relational set-based `ExecuteDelete`; the same sweep covers Cosmos).

See the [Outbox Pattern](https://github.com/Vulthil/Vulthil.SharedKernel/tree/main/docs/articles/outbox-pattern.md)
article for the design, the pluggable-sink model, and the commit-time trigger.
