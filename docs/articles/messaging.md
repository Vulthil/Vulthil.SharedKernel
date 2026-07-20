# Messaging

The messaging packages provide a transport-agnostic abstraction for asynchronous communication between services, with first-class support for RabbitMQ.

## Package Responsibilities

| Package | Role |
|---|---|
| `Vulthil.Messaging.Abstractions` | Consumer and publisher interfaces – reference this from domain/application projects |
| `Vulthil.Messaging` | Queue registration, consumer wiring, hosted orchestration, and the transport-author SDK (`Vulthil.Messaging.Transport`) |
| `Vulthil.Messaging.RabbitMq` | RabbitMQ transport implementation |
| `Vulthil.Messaging.TestHarness` | In-memory transport for integration tests |

The `Vulthil.Messaging.Transport` namespace is a *build-your-own-transport* SDK — see
[Writing a Custom Transport](#writing-a-custom-transport).

## Defining Consumers

### One-way consumer

```csharp
public sealed class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
{
    public Task ConsumeAsync(
        IMessageContext<OrderCreatedEvent> messageContext,
        CancellationToken cancellationToken = default)
    {
        var order = messageContext.Message;
        // Process the event
        return Task.CompletedTask;
    }
}
```

### Request/reply consumer

```csharp
public sealed class GetOrderConsumer : IRequestConsumer<GetOrderRequest, OrderDto>
{
    public Task<OrderDto> ConsumeAsync(
        IMessageContext<GetOrderRequest> messageContext,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new OrderDto());
    }
}
```

The request consumer keeps its strongly-typed `Task<TResponse>` contract — the requester
on the other side will receive a typed `Result<TResponse>`.

## Registering Queues and Consumers

Registration happens in the composition root using the `AddMessaging` builder. Queue
definitions and message configurations are first loaded eagerly from `IConfiguration`,
then merged with whatever code-side calls add; code wins on conflict.

```csharp
builder.AddMessaging(messaging =>
{
    messaging.UseRabbitMq();

    messaging.ConfigureQueue("order-events", queue =>
    {
        queue.AddConsumer<OrderCreatedConsumer>();
        queue.UseRetry(retry =>
        {
            retry.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
            retry.UseJitter(0.2);
        });
        queue.UseDeadLetterQueue();
    });

    messaging.ConfigureQueue("order-requests", queue =>
    {
        queue.AddRequestConsumer<GetOrderConsumer>();
    });
});
```

## Publishing Messages

Inject `IPublisher` to send one-way messages, or `IRequester` for request/reply:

```csharp
public sealed class PlaceOrderHandler(IPublisher publisher)
{
    public async Task HandleAsync(PlaceOrderCommand command, CancellationToken ct)
    {
        // ... create order ...
        await publisher.PublishAsync(new OrderCreatedEvent(order.Id), cancellationToken: ct);
    }
}
```

> **Delivery guarantees.** Publishing uses RabbitMQ publisher confirms: the call awaits the
> broker's acknowledgement and throws if the message is nacked, so a publish the broker never
> accepted does not report success. `Publish` (pub/sub over a fanout/topic exchange) is *not*
> mandatory — zero subscribers is normal — whereas `Send` (point-to-point) *is* mandatory, so a
> missing destination queue surfaces as a failure rather than being silently dropped.

### Publishing from inside a consumer

`IMessageContext` exposes `PublishAsync` directly, so consumers can emit follow-up
messages without injecting `IPublisher`. Correlation metadata
(`CorrelationId`, `ConversationId`, `InitiatorId`) is automatically propagated from
the incoming message to the outgoing one. The optional `configure` callback runs
after auto-propagation, so explicit values override the inherited ones.

```csharp
public sealed class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
{
    public async Task ConsumeAsync(
        IMessageContext<OrderCreatedEvent> ctx,
        CancellationToken cancellationToken = default)
    {
        // Inherits CorrelationId/ConversationId/InitiatorId from ctx
        await ctx.PublishAsync(new InventoryReserveRequested(ctx.Message.OrderId));

        // Or override specific fields explicitly
        await ctx.PublishAsync(new ShippingScheduled(ctx.Message.OrderId), c =>
        {
            c.SetCorrelationId("new-correlation");
            c.AddHeader("priority", "high");
            return ValueTask.CompletedTask;
        });
    }
}
```

`IMessageContext.CancellationToken` exposes the delivery's cancellation token for
handlers that want to observe it alongside the explicit method parameter.

### Header values on the consume side

Custom headers travel as JSON, and the consume side normalizes them so every path — a Vulthil
envelope, a bare-AMQP message from a non-Vulthil producer, or an outbox-relayed publish — surfaces
the same CLR primitives in `IMessageContext.Headers`: strings arrive as `string`, booleans as
`bool`, and numbers as `int`, `long`, or `double` (the narrowest that represents the value). A
header published as `AddHeader("tenant", "acme")` is the string `"acme"` again in the consumer.
Values without a JSON primitive form keep their JSON shape: objects and arrays surface as
`JsonElement`, and types JSON serializes as strings (e.g. `Guid`, `DateTimeOffset`) surface as
that string.

## Point-to-point Send

`IPublisher.PublishAsync` fans a message out via its per-type exchange to any number of
interested consumers. When you need to address a single, named destination — typically
a specific queue on a specific service — use `ISendEndpoint` instead. Sends route
through the broker's default exchange using the destination queue name as the routing
key; topology for that queue is owned by the receiving service and is not declared by
the sender.

Inject `ISendEndpointProvider` and resolve an endpoint by `Uri`:

```csharp
public sealed class OrderRouter(ISendEndpointProvider sendEndpoints)
{
    public async Task RouteAsync(PlaceOrderCommand command, CancellationToken ct)
    {
        var endpoint = await sendEndpoints.GetSendEndpointAsync(new Uri("queue:order-commands"), ct);
        await endpoint.SendAsync(command, ct);
    }
}
```

The default address scheme is `queue:<name>`. Absolute `amqp://`, `amqps://`, and
`rabbitmq://` URIs are also recognized — the queue name is taken from the path. Endpoints
are cached per `Uri` by the provider for the lifetime of the bus.

`MessageConfiguration<T>.CorrelationIdFormatter` still applies on the send path; the
`Exchange` and `RoutingKeyFormatter` settings are intentionally ignored because the
URI is authoritative for the destination.

### Sending from inside a consumer

`IMessageContext` exposes `SendAsync` directly, with the same auto-propagation of
`CorrelationId`/`ConversationId`/`InitiatorId` as `PublishAsync`:

```csharp
public sealed class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
{
    public async Task ConsumeAsync(
        IMessageContext<OrderCreatedEvent> ctx,
        CancellationToken cancellationToken = default)
    {
        await ctx.SendAsync(
            new Uri("queue:fulfillment-commands"),
            new FulfillOrderCommand(ctx.Message.OrderId));
    }
}
```

## Consume Filters

Consume filters wrap the consumer invocation, allowing cross-cutting concerns
(logging, validation, scoped resource management, telemetry, etc.) to be composed
without modifying transport or consumer code. They mirror the ASP.NET Core
middleware shape:

```csharp
public sealed class LoggingConsumeFilter<TMessage> : IConsumeFilter<TMessage>
    where TMessage : notnull
{
    private readonly ILogger<LoggingConsumeFilter<TMessage>> _logger;

    public LoggingConsumeFilter(ILogger<LoggingConsumeFilter<TMessage>> logger)
        => _logger = logger;

    public async Task ConsumeAsync(IMessageContext<TMessage> context, ConsumeDelegate<TMessage> next)
    {
        _logger.LogInformation("Consuming {Type} (correlation={CorrelationId})",
            typeof(TMessage).Name, context.CorrelationId);
        try
        {
            await next(context);
            _logger.LogInformation("Consumed {Type}", typeof(TMessage).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Consume of {Type} failed", typeof(TMessage).Name);
            throw;
        }
    }
}
```

### Registering filters

```csharp
builder.AddMessaging(messaging =>
{
    messaging.UseRabbitMq();

    // Open-generic — applies to every message type
    messaging.AddOpenConsumeFilter(typeof(LoggingConsumeFilter<>));

    // Closed-generic — applies only to OrderCreatedEvent
    messaging.AddConsumeFilter<OrderValidationFilter>();

    messaging.ConfigureQueue("order-events", queue =>
    {
        queue.AddConsumer<OrderCreatedConsumer>();
    });
});
```

Filters are resolved per delivery from the same scope as the consumer, so they
may depend on scoped services (e.g. <c>DbContext</c>, scoped <c>ILogger&lt;T&gt;</c>).
Multiple filters compose in registration order — the first registered is the
outermost.

### Short-circuiting

A filter may skip calling `next` to reject a message:

```csharp
public sealed class TenantGate<TMessage> : IConsumeFilter<TMessage>
    where TMessage : notnull
{
    public Task ConsumeAsync(IMessageContext<TMessage> context, ConsumeDelegate<TMessage> next)
    {
        if (context.Headers.TryGetValue("Tenant", out var t) && t is "blocked")
        {
            // Don't invoke next — consumer is skipped, delivery is acked normally.
            return Task.CompletedTask;
        }
        return next(context);
    }
}
```

For request/reply consumers, short-circuiting causes the requester to receive a
`Result<TResponse>` failure (with an explanatory error) instead of timing out.

### Built-in filters

`AddMessaging` registers a default open-generic `LoggingConsumeFilter<TMessage>` as
the outermost filter in the pipeline. It emits structured Debug logs at consume
entry/exit and a Warning log on uncaught exceptions, with timing information:

```
dbug: Consuming Acme.Orders.OrderCreatedEvent (messageId=..., correlationId=...)
dbug: Consumed Acme.Orders.OrderCreatedEvent (messageId=...) in 12ms
```

User-registered filters compose INSIDE the defaults, so the logging filter wraps
every other filter and the consumer itself.

Toggle the built-in filter via `MessagingOptions.ConsumeFilters`:

```json
{
  "Messaging": {
    "Options": {
      "ConsumeFilters": { "EnableLogging": false }
    }
  }
}
```

Or in code:

```csharp
m.ConfigureMessagingOptions(opts => opts.ConsumeFilters.EnableLogging = false);
```

The toggle is applied when `AddMessaging` composes the pipeline: a disabled filter is
never registered in DI at all (there is no per-delivery flag check), so flipping the
flag after registration has no effect.

### Idempotent receivers (inbox)

`Vulthil.Messaging.Inbox` ships a consume filter that deduplicates redelivered
messages on top of at-least-once delivery. The consumer invocation is handed to an
`IIdempotencyStore`, which owns the unit: with a transactional (relational) store the
processed-marker and the consumer's writes commit atomically — exactly-once
processing — while a store without cross-partition transactions (e.g. Cosmos) is
effectively-once. Opt a message type in with `AddIdempotentInbox<TMessage>()`. See
the [Inbox Pattern](inbox-pattern.md) article for the full design.

## Ordered Processing (per-aggregate)

Without ordering controls a queue processing messages concurrently does not preserve
order. `UsePartitioner<TMessage>` restores **per-aggregate ordering**: deliveries that
share a partition key are processed one at a time and in publish order, while deliveries
with different keys run concurrently.

```csharp
builder.AddMessaging(m =>
{
    // Order OrderUpdated deliveries per OrderId across 16 lanes.
    m.UsePartitioner<OrderUpdated>(partitionCount: 16, ctx => ctx.Message.OrderId.ToString());

    // Shorthand: omit the selector to key on CorrelationId (the natural key when it
    // carries the aggregate id). Equivalent to passing ctx => ctx.CorrelationId.
    m.UsePartitioner<OrderUpdated>(16);

    m.ConfigureQueue("orders", q => q.AddConsumer<OrderUpdatedConsumer>());
});
```

Share one `Partitioner` across several message types to serialize messages correlated
to the same key regardless of their type (e.g. a saga). The selector is optional here too
and defaults to `CorrelationId`:

```csharp
var orders = new Partitioner(16);
m.UsePartitioner<OrderUpdated>(orders);
m.UsePartitioner<OrderShipped>(orders);
```

### How it works

Ordering is enforced by the RabbitMQ transport, not a consume filter. When a queue
consumes a partitioned message type, its worker:

1. Receives deliveries from a **single channel in FIFO order** (`consumerDispatchConcurrency = 1`),
   so the partition key is read and the delivery assigned to its lane in arrival order.
2. Hands each delivery to the key's lane and immediately returns, so the next delivery is
   laned in order while lanes process **concurrently** (cross-key parallelism).
3. **Acknowledges each message when its lane finishes** (deferred ack). `PrefetchCount`
   bounds the number of in-flight deliveries and therefore the effective parallelism.

Notes:

- For a partitioned queue, `ConcurrencyLimit` does not drive dispatch (it is forced to
  ordered single dispatch); tune throughput with `PrefetchCount` instead.
- A delivery whose selected key is `null` or empty is processed without lane
  serialization (it still runs off the receive loop, so it does not block ordering of
  other keys).
- The partition count affects only fan-out (how many distinct keys progress at once),
  never correctness. The lane hash is in-process, so a key's lane need not be stable
  across processes.
- The partitioner orders deliveries **within one process**. Ordering across load-balanced
  consumer instances additionally requires a single active consumer (see *Ordering across
  instances* below), which partitioned queues enable automatically.
- **Failure path:** a partitioned queue retries **in-memory** automatically — the consumer
  is re-invoked in-process while the delivery (and its lane) is held — so a failing message
  cannot be overtaken by a later same-key message. See *Retries* below.

### Ordering across instances (single active consumer)

The partitioner serializes same-key deliveries inside a single process. When the same queue is
consumed by several load-balanced instances, the broker round-robins deliveries between them and
same-key messages can again be processed concurrently. RabbitMQ's **single active consumer**
closes that gap: the broker keeps exactly one consumer active and promotes a standby consumer only
if the active one disconnects, so ordering is preserved and the queue fails over without manual
intervention.

Partitioned queues turn this on automatically. Any queue can opt in explicitly:

```csharp
m.ConfigureQueue("orders", q =>
{
    q.UseSingleActiveConsumer();
    q.AddConsumer<OrderUpdatedConsumer>();
});
```

Notes and trade-offs:

- **No scale-out for that queue.** Only one consumer works at a time, so adding instances buys
  failover, not throughput. To scale a partitioned workload across instances, shard into multiple
  queues (one per partition) and bind each instance to its own — a larger change that is out of
  scope here. This mirrors MassTransit, whose in-process partitioner is likewise single-instance.
- **Existing queues.** The single-active-consumer flag is a queue argument fixed at declaration.
  Enabling it on a queue that already exists fails declaration with `406 PRECONDITION_FAILED`;
  delete and recreate the queue to change it.
- **At-least-once on failover.** When the active consumer dies mid-delivery, unacknowledged
  messages are redelivered to the promoted consumer, so a handler may observe a message more than
  once. Make handlers idempotent; broker-level exactly-once delivery is not provided.

## Retries

Retry policies are resolved **per consumer**. A consumer's effective policy is its own
`AddConsumer<TConsumer>(c => c.UseRetry(...))` when configured, otherwise the queue-level
`q.UseRetry(...)` default; a consumer with neither fails terminally on its first failure. The
resolution also applies to polymorphic registrations — a policy configured on
`IConsumer<IOrderEvent>` governs deliveries of every subscribed concrete implementer.

Request consumers never retry: a thrown exception is immediately returned to the requester as an
RPC fault reply (see [Request/Reply](#requestreply)), so `UseRetry` inside
`AddRequestConsumer(...)` throws at configuration time, and a queue-level default does not apply
to them. Retry a failed request on the requesting side if needed.

There are two execution modes:

- **Delayed re-delivery (default):** a failed message is re-published to the queue's retry
  exchange with a delay and re-delivered later. Good for back-off without holding a consumer,
  but it **reorders** relative to other messages.
- **In-memory:** the consumer is re-invoked in-process while the delivery is held, preserving
  order. Opt in with `q.UseRetry(r => { r.Immediate(3); r.InMemory(); })`. Partitioned queues
  use in-memory retry **automatically**, since ordering requires it.

### Several consumers on one delivery

When a queue runs several consumers for the same message, one consumer's failure neither skips
the others in that attempt nor re-runs the ones that already succeeded:

- Every pending consumer runs once per attempt; failures are collected per consumer.
- On retry, **only the consumers that failed are re-dispatched** — on the delayed path the
  re-publish stamps the failed consumers' identities into an `x-retry-handlers` header and the
  re-delivery dispatches just those (an identity that no longer matches — the consumer was renamed
  or removed mid-retry — is logged and skipped).
- Retries advance in shared rounds counted by `x-retry-count`, and each consumer's budget and
  back-off come from **its own** policy. Consumers retrying in the same round share the actual
  wait: the longest delay any of their policies requests, so no consumer retries earlier than its
  own back-off asks (it may retry later).
- The delivery is held in-process (in-memory mode) when the queue is partitioned or any retrying
  consumer's policy is in-memory; otherwise it goes through the retry queue.
- A consumer that exhausts its retries (or has no policy, or threw an ignored exception) fails
  terminally: its `Fault<T>` is published immediately, while the delivery stays live for any
  consumers still retrying.

On exhaustion the message is dead-lettered (when a dead-letter queue is configured) in both
modes — precisely, the delivery is nacked for dead-lettering when its **final** retry round ends
with a terminal failure. A consumer that failed terminally in an earlier round (while others kept
retrying and eventually succeeded) is reported through its published fault, not through the
dead-letter queue.

### Ignored exceptions

`r.Ignore<TException>()` (or the `IgnoreExceptions` list in configuration) exempts exception
types from retrying — a matching failure is immediately terminal. The ignore list is resolved
once, on first use; config-supplied names outside the core library must be **assembly-qualified**
(`"Acme.Orders.PricingException, Acme.Orders"`), and a name that cannot be resolved is skipped
with a startup warning — the exceptions it was meant to exclude are retried.

> **Head-of-line caveat (delayed re-delivery):** the delayed retry uses one shared retry queue with a
> per-message TTL, and RabbitMQ expires messages only from the *head* of a queue. With variable or jittered
> back-off intervals, a long-delay message at the head holds back shorter-delayed messages queued behind it.
> For predictable timing, use a single fixed interval (uniform TTL) or in-memory retry (`r.InMemory()`), which
> holds the delivery in-process and is unaffected.

## Faults

When a consumer fails terminally — its retries exhausted, or it has no retry policy — a `Fault<T>`
is published **by convention** to a shared topic exchange (default `Fault.Exchange`, configurable via
`Messaging:Options:FaultExchangeName`). Faults are per consumer: one fault is published for each
terminally-failed consumer, so a delivery dispatched to several consumers can produce several faults,
each carrying that consumer's exception. No per-message opt-in by the producer is required, so faults
are observable broker-side without changing any producer. The faulted message's URN is the routing
key, so an operator binds a queue to the fault exchange — `#` for every fault, or a specific URN to
filter by faulted message type — and reads the payload. The fault body is a `Fault<T>` JSON document
(the AMQP `type` is `Fault<{original-urn}>`):

```csharp
public record Fault<TMessage> where TMessage : notnull
{
    public required TMessage Message { get; init; }            // the original message body
    public required string ExceptionMessage { get; init; }
    public required string? StackTrace { get; init; }
    public required string ExceptionType { get; init; }
    public required DateTimeOffset FaultedAt { get; init; }
    public required MessageContextSnapshot OriginalContext { get; init; } // original transport metadata
}
```

`Message` is the faulted message's own payload — for envelope-wrapped (Vulthil-produced) deliveries
the wire envelope is unwrapped before the fault is built — so a subscriber reads the original fields
with a plain deserialization:

```csharp
var fault = JsonSerializer.Deserialize<Fault<OrderCreatedEvent>>(body, jsonOptions)!;
var orderId = fault.Message.OrderId;
```

The fault exchange is a diagnostics/observability broadcast — drain it with a monitoring service or
any AMQP consumer bound to the exchange — rather than a typed `IConsumer<Fault<T>>` endpoint.

A message can override the routing per-message: if it carries an explicit `FaultAddress`, the fault is
routed **point-to-point** to that address (through the broker's default exchange) instead of being
broadcast to the fault exchange — exactly one fault per terminally-failed consumer is emitted either
way. Set it on publish:

```csharp
await publisher.PublishAsync(new OrderCreatedEvent(orderId), ctx =>
{
    ctx.SetFaultAddress(new Uri("queue:order-faults"));
    return ValueTask.CompletedTask;
});
```

Fault publishing is best-effort: a failure to publish the fault is logged and never prevents the
original delivery from being settled (nacked for dead-lettering).

### Poison deliveries

Deliveries that fail **before** a consumer can run bypass the retry and fault machinery entirely, and
no `Fault<T>` is published for them:

- **Unknown message type** (no consumer/plan matches the delivery's type identity): the delivery is
  **acked and dropped permanently**, with a warning log — it does not reach the dead-letter queue.
- **Undeserializable body** (malformed JSON, or a body that deserializes to `null`): the delivery is
  **nacked without requeue**, with an error log. When the queue has a dead-letter queue configured
  (`q.UseDeadLetterQueue()`), the broker moves it there; otherwise it is discarded.

## Routing Keys

Routing keys flow through two distinct configuration sites, one on each side of the wire:

- **Producer side** — `MessageConfiguration<TMessage>.UseRoutingKey(selector)` controls the key the
  publisher writes onto each outgoing message.
- **Consumer side** — `q.Subscribe<TMessage>(routingKey)` controls the binding pattern the broker
  uses to filter deliveries for the queue.

The two sites can use the same value (typical for direct exchanges) or different values (e.g. a topic
binding `order.*` matching producer keys like `order.created`). When the binding pattern is left null,
the broker receives an empty pattern: fanout/headers exchanges ignore it; direct/topic exchanges only
match an empty published key — supply a specific pattern explicitly when needed.

Exchange-type choice belongs to the **message** exchange (`MessageConfiguration<TMessage>.ExchangeType`),
which is where `Subscribe` binding patterns filter. The queue's **own** exchange
(`QueueDefinition.ExchangeType`) must stay `Fanout`: the queue is bound to it with an empty routing key,
and retry re-deliveries dead-letter back into it carrying the original routing key, so a Direct, Topic,
or Headers queue exchange would silently drop both normal and retry deliveries. The RabbitMQ transport
rejects a non-fanout queue exchange when the host starts.

```csharp
// Producer side: what the publisher writes on the wire.
messaging.ConfigureMessage<OrderCreatedEvent>(message =>
{
    message.UseRoutingKey(e => $"order.{e.Region}");
    message.UseCorrelationId(e => e.OrderId.ToString());
});

// Consumer side: how the queue binds. Pattern matches the producer's published key.
messaging.ConfigureQueue("order-projector", q =>
{
    q.Subscribe<OrderCreatedEvent>("order.*");
    q.AddConsumer<OrderProjector>();
});
```

## Message Configuration

Each message type is associated with a `MessageConfiguration` that controls the
exchange name, exchange type, durability, routing/correlation formatters used
when publishing, and the stable wire URN. The `Exchange` defaults to the message
CLR full type name when constructed via `MessageConfiguration<TMessage>`; the
publisher and bus topology share that same source of truth, so they never get
out of sync.

```csharp
messaging.ConfigureMessage<OrderCreatedEvent>(m =>
{
    m.Exchange = "orders.events";            // override default of typeof().FullName
    m.ExchangeType = MessagingExchangeType.Topic;
    m.Durable = true;
    m.UseRoutingKey(e => $"order.{e.Region}");
});
```

### Wire identity (URN)

Every message type carries a stable wire URN that identifies it on the broker
independent of its CLR type name. The default is derived from the CLR type:

```
urn:message:Acme.Orders:OrderCreatedEvent
```

Override it via `MessageConfiguration<T>.Urn` to keep the wire identity stable
across CLR renames — producers and consumers on different versions agree on the
URN even if their C# class names diverge:

```csharp
messaging.ConfigureMessage<OrderCreatedEvent>(m =>
{
    m.Urn = new Uri("urn:message:Acme.Orders:OrderPlaced");
});
```

The URN is the dispatch key on the receive side — it appears in the message
envelope's `messageType` field, and `MessageExecutionRegistry<THandler>` keys its
execution plans by URN.

### Message envelope (wire format)

Vulthil-produced messages are wrapped in a JSON envelope with explicit metadata
fields rather than relying on AMQP `BasicProperties` headers:

```json
{
  "messageId":      "01931d7e-...",
  "correlationId":  "a3f1...",
  "conversationId": "a3f1...",
  "initiatorId":    "01931d7d-...",
  "sourceAddress":  "queue:order-service",
  "messageType":    "urn:message:Acme.Orders:OrderPlaced",
  "message":        { "orderId": "abc", "amount": 100 },
  "sentTime":       "2026-05-27T12:00:00Z",
  "headers":        { "tenant": "acme" }
}
```

`BasicProperties.MessageId`, `CorrelationId`, and `Type` (= URN) are still
mirrored into AMQP for broker tooling and trace propagation, but the envelope
is the source of truth.

External producers that emit bare JSON (no envelope) are accepted on the
receive path — the worker probes the body and falls back to using
`BasicProperties.Type` as the type identity.

## Subscriptions and Polymorphism

Topology and dispatch are separated:

- **Subscriptions** = exchange bindings. A queue is subscribed to a concrete
  message type via `q.Subscribe<TConcrete>()`; at topology setup time, the
  queue's binding to `TConcrete`'s exchange is declared.
- **Consumers** = handlers. A consumer is registered via
  `q.AddConsumer<TConsumer>()` where `TConsumer` implements
  `IConsumer<TMessage>` — `TMessage` may be a concrete type, an interface, or
  an abstract base.

When `TMessage` is **concrete**, the consumer's message type is auto-subscribed
at build time — preserves the today's ergonomic for the simple case:

```csharp
m.ConfigureQueue("orders", q => q.AddConsumer<OrderCreatedConsumer>());
// → q is auto-subscribed to OrderCreatedEvent.
```

When `TMessage` is **polymorphic** (an interface or abstract base), the
consumer fires for any concrete delivery whose CLR type is assignable to it —
but the queue must explicitly subscribe to the concrete types it wants to bind:

```csharp
// OrderProjector : IConsumer<IOrderEvent>
m.ConfigureQueue("order-projector", q =>
{
    q.Subscribe<OrderPlaced>();      // bind queue to OrderPlaced exchange
    q.Subscribe<OrderCancelled>();   // bind queue to OrderCancelled exchange
    q.AddConsumer<OrderProjector>(); // fires on either delivery
});
```

`SubscribeAll<TInterface>(Assembly)` discovers concrete implementers in the
supplied assembly and calls `Subscribe<TConcrete>` for each — abstract types
and interfaces are skipped (only concrete types have exchanges):

```csharp
m.ConfigureQueue("order-projector", q =>
{
    q.SubscribeAll<IOrderEvent>(typeof(OrderPlaced).Assembly);
    q.AddConsumer<OrderProjector>();
});
```

Dispatch is transitive: with a hierarchy `OrderPlaced : IOrder : IOrderEvent`,
a single `OrderPlaced` delivery fires consumers registered against the concrete
`OrderPlaced`, against `IOrder` (immediate interface), and against
`IOrderEvent` (transitive interface).

### Validation at composition time

After `ConfigureQueue` returns, a build pass validates the queue's wiring and
throws with a descriptive message if anything is incoherent:

- A consumer with no matching concrete subscription (e.g. `IConsumer<IOrderEvent>`
  but no `Subscribe<TConcrete>` for any implementer).
- A subscription with no matching consumer.
- A request consumer registered against an abstract or interface request type
  (responses are typed and can't depend on the concrete request type).

These failures surface at app startup, not at the first message delivery.

### Request consumers stay non-polymorphic

`IRequestConsumer<TRequest, TResponse>` requires a concrete `TRequest` — the
response type is fixed and can't be selected by the incoming concrete type.
Each `(queue, message type)` pair admits at most one request consumer; a
second one fails fast at composition.

`MessageConfiguration` instances can also come from configuration — see below.

## Configuration-driven Setup

Queue and message settings can be defined entirely in `appsettings.json`. The
`AddMessaging` call loads every section under `Messaging:Queues:*` and
`Messaging:Messages:*` into the runtime before running the configurator action.
Subsequent `ConfigureQueue` / `ConfigureMessage<T>` calls mutate the loaded
instances, with code taking precedence on conflict.

```json
{
  "Messaging": {
    "Options": {
      "DefaultTimeout": "00:00:30",
      "FaultExchangeName": "Fault.Exchange"
    },
    "Queues": {
      "order-events": {
        "PrefetchCount": 64,
        "ChannelCount": 2,
        "ConcurrencyLimit": 4,
        "IsQuorum": true,
        "DefaultRetryPolicy": {
          "MaxRetryCount": 3,
          "JitterFactor": 0.2,
          "Intervals": [ "00:00:01", "00:00:05", "00:00:30" ]
        }
      }
    },
    "Messages": {
      "Acme.Orders.OrderCreatedEvent": {
        "Exchange": "orders.events",
        "ExchangeType": "Topic",
        "Durable": true
      }
    }
  }
}
```

### Config-only setup

A service can declare its topology purely in `appsettings.json` and skip the code
side entirely — useful for publisher-only services or environments where queue
shape is owned by ops:

```csharp
builder.AddMessaging(m => m.UseRabbitMq());
```

### Code-only override

Code values always win over configuration values:

```csharp
builder.AddMessaging(m =>
{
    m.UseRabbitMq();
    m.ConfigureQueue("order-events", q =>
    {
        q.ConfigureQueue(d => d.PrefetchCount = 128);  // overrides appsettings value
        q.AddConsumer<OrderCreatedConsumer>();
    });
});
```

### Merged

The common case — topology from config, consumer wiring from code:

```json
{ "Messaging": { "Queues": { "order-events": { "PrefetchCount": 64 } } } }
```

```csharp
m.ConfigureQueue("order-events", q => q.AddConsumer<OrderCreatedConsumer>());
// PrefetchCount=64 (from config) + OrderCreatedConsumer registration (from code)
```

## Observability

The RabbitMQ transport emits an `ActivitySource` named `"Vulthil.Messaging.RabbitMq"`
with `Producer`/`Consumer` spans for publish, request, and receive operations. Tag
conventions follow the OpenTelemetry messaging semantic conventions, with a few
Vulthil-specific tags (`vulthil.messaging.message_type`, `.consumer_type`,
`.retry_count`, `.queue`).

`UseRabbitMq` registers the source with the application's `TracerProvider`
automatically, gated on the Aspire client's `DisableTracing` setting — so disabling
RabbitMQ tracing in Aspire suppresses the Vulthil spans too. If you bring your own
`TracerProvider` configuration, you can register the source manually:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddVulthilMessagingInstrumentation());
```

W3C trace context (`traceparent` / `tracestate`) propagation is handled by
`RabbitMQ.Client` itself, so producer-side activities link to consumer-side
activities on the receiving service without any extra setup.

## Health Checks

`UseRabbitMq` also registers a startup health check named
`"vulthil_messaging_rabbitmq_bus"` (tagged `ready`, `messaging`, `rabbitmq`). It
reports:

- `Unhealthy("starting")` until `RabbitMqBus.StartAsync` completes (topology
  declaration + consumer registration finished). A transient startup failure —
  for example a broker that is still coming up — is retried with backoff until it
  succeeds or the host stops, so the probe stays `Unhealthy("starting")` (and each
  failed attempt is logged) instead of faulting the host.
- `Healthy("started")` after a successful startup.

Registration is gated on the Aspire client's `DisableHealthChecks` setting; set
that to `true` to suppress the health check alongside Aspire's connection-level
health check.

## Request/Reply

`IRequester` is registered automatically by `UseRabbitMq` and returns a typed
`Result<TResponse>`:

```csharp
public sealed class OrderLookupService(IRequester requester)
{
    public Task<Result<OrderDto>> GetAsync(Guid orderId, CancellationToken ct)
        => requester.RequestAsync<GetOrderRequest, OrderDto>(
            new GetOrderRequest(orderId), cancellationToken: ct);
}
```

The reply queue is created lazily on the first request, so producer-only services
that never call `RequestAsync` do not declare any reply infrastructure.

### Configuring the request

`RequestAsync` accepts an optional `Func<IRequestContext, Task>` to configure the
outgoing request. `IRequestContext` extends `IPublishContext` — so the routing key,
correlation id, and headers can be set just like a publish — and adds
`SetTimeout(TimeSpan)` for overriding the response timeout on a per-request basis:

```csharp
var result = await requester.RequestAsync<GetOrderRequest, OrderDto>(
    new GetOrderRequest(orderId),
    ctx =>
    {
        ctx.SetTimeout(TimeSpan.FromSeconds(5));
        ctx.AddHeader("priority", "high");
        return Task.CompletedTask;
    },
    cancellationToken: ct);
```

When no timeout is set on the context, the request falls back to
`Messaging:Options:DefaultTimeout` (see
[Configuration-driven Setup](#configuration-driven-setup)). A request that exceeds
its timeout completes with a `Result<TResponse>` failure carrying the
`Messaging.Request.Timeout` error code rather than throwing.

### Reply wire format & correlation

Each request carries a dedicated **request id** (a fresh GUID per call) in the AMQP
`CorrelationId` property and the envelope's `requestId` field. The reply echoes it, and the
requester correlates the reply to the awaiting call by this id — independently of the business
`CorrelationId` (set via `UseCorrelationId` or `SetCorrelationId`), which is therefore free to
repeat across concurrent requests without colliding.

The reply is a normal `MessageEnvelope` (single-serialized, like every other message):

- **Success** carries the `TResponse` payload at the response type's URN.
- **Failure** carries an RPC fault at `urn:message:Vulthil:RpcFault` (the remote exception's
  type and message); the requester maps it to a `Result<TResponse>` failure with the
  `Messaging.Request.Failure` error code.

A request consumer runs exactly once per request: a thrown exception becomes the fault reply
rather than entering the retry machinery, so retry policies do not apply to request consumers
(see [Retries](#retries)) — retry on the requesting side when a request should be re-attempted.

## Writing a Custom Transport

`Vulthil.Messaging` is also a *build-your-own-transport* SDK. The transport-agnostic
primitives live in the **`Vulthil.Messaging.Transport`** namespace, so a transport for a
broker other than RabbitMQ can be written in a separate package against the public surface
alone — the RabbitMQ transport uses nothing more.

A transport is the glue between the broker and these primitives:

| Concern | Primitive |
|---|---|
| Lifetime | `ITransport.StartAsync` — declare topology, then start consuming |
| Execution plans | `MessageExecutionRegistry<THandler>` + your `IMessageHandlerFactory<THandler>` |
| Wire format | `MessageEnvelope` + `MessageEnvelopeFactory.Create` |
| Receive context | `MessageContext.CreateFromEnvelope` |
| Filter pipeline | `ConsumePipelineFactory.Build` |
| RPC failures | `RpcFault` |

### 1. Build execution plans

Choose a `THandler` type for your transport's dispatch closure, then implement
`IMessageHandlerFactory<THandler>` to turn each registration into one. The factory is where the
message type is statically known, so it is also where you compose the filter pipeline and build
the receive context:

```csharp
public delegate Task Dispatch(IServiceProvider scope, object message, MessageEnvelope envelope, CancellationToken ct);

internal sealed class MyHandlerFactory : IMessageHandlerFactory<Dispatch>
{
    public HandlerEntry<Dispatch> ForConsumer(Type consumer, Type message, RetryPolicyDefinition? retry)
        => new(BuildConsumer(consumer, message), HandlerKind.Consumer);

    public HandlerEntry<Dispatch> ForRequestConsumer(Type consumer, Type request, Type response, RetryPolicyDefinition? retry)
        => new(BuildRequestConsumer(consumer, request, response), HandlerKind.RequestConsumer);

    // Bound generically (e.g. via reflection) so TMessage is known here:
    private static Dispatch Consumer<TConsumer, TMessage>() where TConsumer : class, IConsumer<TMessage> where TMessage : notnull
        => async (scope, message, envelope, ct) =>
        {
            var consumer = scope.GetRequiredService<TConsumer>();
            var context = MessageContext.CreateFromEnvelope(
                (TMessage)message, envelope, routingKey: "", redelivered: false,
                retryCount: 0, replyToFallback: null,
                scope.GetRequiredService<IPublisher>(), scope.GetRequiredService<ISendEndpointProvider>(), ct);

            var pipeline = ConsumePipelineFactory.Build<TMessage>(scope, c => consumer.ConsumeAsync(c, c.CancellationToken));
            await pipeline(context);
        };
}
```

Let `MessageExecutionRegistry<THandler>` assemble the per-message-type plans from the configured
queues — it handles URN keying, polymorphic fan-out, deduplication, request-consumer uniqueness
(at most one per message type per queue), and partition attachment. Plans are keyed by URN within
a registry instance, so queues registered into the same instance accumulate their handlers into
one plan per message type. Register every queue in a single instance only when your transport
dispatches each produced message exactly once (as the in-memory test harness does); a transport
that receives a distinct delivery per queue (as the RabbitMQ transport does) must build one
registry per queue so a delivery dispatches only the handlers its own queue registered:

```csharp
var registry = new MessageExecutionRegistry<Dispatch>(provider, new MyHandlerFactory());
foreach (var queue in provider.QueueDefinitions)
{
    registry.RegisterQueue(queue);
}
```

### 2. Produce

Wrap each outgoing message in a `MessageEnvelope`. `MessageEnvelopeFactory.Create` promotes the
publish context's metadata to typed envelope fields and serializes the payload:

```csharp
var envelope = MessageEnvelopeFactory.Create(
    message, publishContext, messageId, correlationId, urn, provider.JsonSerializerOptions);
var body = JsonSerializer.SerializeToUtf8Bytes(envelope, provider.JsonSerializerOptions);
```

`PublishContext`/`RequestContext` implement the `IPublishContext`/`IRequestContext` the caller's
`configure` callback writes to; read their resolved properties (`RoutingKey`, `CorrelationId`,
`Headers`, …) when building the broker message.

### 3. Consume

In the receive loop, parse the envelope, resolve the plan by URN, deserialize the payload, then
run the plan's handlers:

```csharp
var envelope = JsonSerializer.Deserialize<MessageEnvelope>(body, provider.JsonSerializerOptions)!;
var plan = registry.GetPlanByUrn(envelope.MessageType);
if (plan is null) { return; } // unknown type — drop or dead-letter

var message = envelope.Message.Deserialize(plan.MessageType.Type, provider.JsonSerializerOptions)!;

await using var scope = scopeFactory.CreateAsyncScope();
foreach (var dispatch in plan.Handlers)
{
    await dispatch(scope.ServiceProvider, message, envelope, ct);
}
```

When `plan.IsPartitioned`, serialize same-key deliveries through `plan.Partition` so per-key order
is preserved (the RabbitMQ transport lanes deliveries through a `Partitioner`). The
`MessageEnvelope` also carries metadata for the bare-JSON fallback — resolve unknown types via
`provider.GetMessageType(urn)` / `registry.GetPlan(typeName)`.

### 4. RPC replies

A request consumer replies with a `MessageEnvelope`: the `TResponse` payload at the response
type's URN on success, or an `RpcFault` at `RpcFault.UrnUri` on failure. Keeping the envelope and
`RpcFault` shapes identical across transports means Vulthil clients interoperate without a
transport-specific reply contract:

```csharp
var fault = new RpcFault
{
    Message = ex.Message,
    ExceptionType = ex.GetType().FullName!,
    StackTrace = ex.StackTrace,
    FaultedAt = DateTimeOffset.UtcNow,
};
var reply = new MessageEnvelope
{
    MessageType = RpcFault.UrnUri,
    Message = JsonSerializer.SerializeToElement(fault, provider.JsonSerializerOptions),
    RequestId = request.RequestId,
};
```

## Testing Messaging

`Vulthil.Messaging.TestHarness` provides an in-memory transport that runs your consumers with no broker and
captures produced and consumed messages for assertion. Compose it with `UseTestHarness()` (in place of a broker
transport) or swap an existing transport in an integration test with `ReplaceTransportWithTestHarness()`:

```csharp
builder.AddMessaging(messaging =>
{
    messaging.ConfigureQueue("orders", q => q.AddConsumer<OrderCreatedConsumer>());
    messaging.UseTestHarness();
});

// ...after building the host and publishing:
var harness = host.Services.GetRequiredService<ITestHarness>();
harness.Published<OrderCreatedEvent>().ShouldHaveSingleItem().Message.OrderId.ShouldBe(expectedOrderId);
harness.Consumed<OrderCreatedEvent>().ShouldHaveSingleItem();
```

The harness is built entirely on the `Vulthil.Messaging.Transport` SDK above, so it is also a worked example of a
custom transport. See [Testing](testing.md#messaging-test-harness) for the full API (`Published`/`Sent`/
`Consumed`/`Requested`, the `Respond`/`Handle` response stubs, and the integration-test swap).
