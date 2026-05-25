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

## Routing Keys

Routing keys control which consumers receive a message on topic exchanges.

### Attribute-based routing

```csharp
[RoutingKey("order.created")]
public sealed class OrderCreatedConsumer : IConsumer<OrderCreatedEvent> { ... }
```

### Dynamic routing keys

```csharp
messaging.ConfigureMessage<OrderCreatedEvent>(message =>
{
    message.UseRoutingKey(e => $"order.{e.Region}");
    message.UseCorrelationId(e => e.OrderId.ToString());
});
```

## Message Configuration

Each message type is associated with a `MessageConfiguration` that controls the
exchange name, exchange type, durability, and routing/correlation formatters used
when publishing. The `Exchange` defaults to the message CLR full type name when
constructed via `MessageConfiguration<TMessage>`; the publisher and bus topology
share that same source of truth, so they never get out of sync.

```csharp
messaging.ConfigureMessage<OrderCreatedEvent>(m =>
{
    m.Exchange = "orders.events";            // override default of typeof().FullName
    m.ExchangeType = MessagingExchangeType.Topic;
    m.Durable = true;
    m.UseRoutingKey(e => $"order.{e.Region}");
});
```

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

## Testing Messaging

`Vulthil.Messaging.TestHarness` provides an in-memory transport that captures
published messages for assertion:

```csharp
var published = testHarness.Published<OrderCreatedEvent>();
Assert.Single(published);
Assert.Equal(expectedOrderId, published.First().Message.OrderId);
```

See [Testing](testing.md) for more details on integration test setup.
