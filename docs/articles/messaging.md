# Messaging

The messaging packages provide a transport-agnostic abstraction for asynchronous communication between services, with first-class support for RabbitMQ.

## Package Responsibilities

| Package | Role |
|---|---|
| `Vulthil.Messaging.Abstractions` | Consumer and publisher interfaces – reference this from domain/application projects |
| `Vulthil.Messaging` | Queue registration, consumer wiring, and hosted service orchestration |
| `Vulthil.Messaging.RabbitMq` | RabbitMQ transport implementation |
| `Vulthil.Messaging.TestHarness` | In-memory transport for integration tests |

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

The filter stays registered in DI; only its behavior is skipped, so it's still
resolvable in unit tests if you want to assert against it.

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

    // The CorrelationId is the natural key when it carries the aggregate id.
    m.UsePartitioner<OrderUpdated>(16, ctx => ctx.CorrelationId);

    m.ConfigureQueue("orders", q => q.AddConsumer<OrderUpdatedConsumer>());
});
```

Share one `Partitioner` across several message types to serialize messages correlated
to the same key regardless of their type (e.g. a saga):

```csharp
var orders = new Partitioner(16);
m.UsePartitioner<OrderUpdated>(orders, ctx => ctx.CorrelationId);
m.UsePartitioner<OrderShipped>(orders, ctx => ctx.CorrelationId);
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

- For a partitioned queue, `ConcurrencyLimit` no longer drives dispatch (it is forced to
  ordered single dispatch); tune throughput with `PrefetchCount` instead.
- A delivery whose selected key is `null` or empty is processed without lane
  serialization (it still runs off the receive loop, so it does not block ordering of
  other keys).
- The partition count affects only fan-out (how many distinct keys progress at once),
  never correctness. The lane hash is in-process, so a key's lane need not be stable
  across processes.
- This preserves order on a **single instance**. Ordering across load-balanced consumers
  additionally requires a single active consumer per partition (a later enhancement),
  mirroring MassTransit's model.
- **Failure path:** a partitioned queue retries **in-memory** automatically — the consumer
  is re-invoked in-process while the delivery (and its lane) is held — so a failing message
  cannot be overtaken by a later same-key message. See *Retries* below.

## Retries

`q.UseRetry(...)` configures the retry policy for a queue's consumers. There are two
execution modes:

- **Delayed re-delivery (default):** a failed message is re-published to the queue's retry
  exchange with a delay and re-delivered later. Good for back-off without holding a consumer,
  but it **reorders** relative to other messages.
- **In-memory:** the consumer is re-invoked in-process while the delivery is held, preserving
  order. Opt in with `q.UseRetry(r => { r.Immediate(3); r.InMemory(); })`. Partitioned queues
  use in-memory retry **automatically**, since ordering requires it.

On exhaustion the message is dead-lettered (when a dead-letter queue is configured) in both
modes.

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
envelope's `messageType` field, and `MessageTypeCache` keys its execution plans
by URN.

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
  declaration + consumer registration finished).
- `Healthy("started")` after a successful startup.
- `Unhealthy(...)` with the original exception if startup fails.

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

## Testing Messaging

`Vulthil.Messaging.TestHarness` provides an in-memory transport that captures
published messages for assertion:

```csharp
var published = testHarness.Published<OrderCreatedEvent>();
Assert.Single(published);
Assert.Equal(expectedOrderId, published.First().Message.OrderId);
```

See [Testing](testing.md) for more details on integration test setup.
