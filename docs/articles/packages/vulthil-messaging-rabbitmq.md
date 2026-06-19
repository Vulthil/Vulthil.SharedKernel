# Vulthil.Messaging.RabbitMq

Use `Vulthil.Messaging.RabbitMq` to run the messaging abstractions over RabbitMQ.

## When to use

- RabbitMQ exchanges/queues as the transport
- Production messaging with broker-backed delivery

## Pattern

- Keep RabbitMQ-specific settings near startup wiring
- Tune exchange/queue settings per workload
- Keep business handlers transport-agnostic

## Usage

### Registration

```csharp
builder.AddMessaging(messaging =>
{
    messaging.UseRabbitMq();

    messaging.ConfigureQueue("order-events", queue =>
    {
        queue.AddConsumer<OrderCreatedConsumer>();
        queue.ConfigureQueue(q =>
        {
            q.PrefetchCount = 32;
            q.ExchangeType = MessagingExchangeType.Topic;
        });
    });
});
```

### Queue settings via configuration

Queue settings can be bound from `appsettings.json` under `Messaging:Queues:{name}`:

```json
{
  "Messaging": {
    "Queues": {
      "order-events": {
        "PrefetchCount": 64,
        "ChannelCount": 2,
        "ConcurrencyLimit": 4
      }
    }
  }
}
```

### Effective concurrency

The three queue knobs combine: the broker keeps up to **`ChannelCount × PrefetchCount`** messages in flight for
the queue, and up to **`ChannelCount × ConcurrencyLimit`** consumer handlers run in parallel (each channel
dispatches `ConcurrencyLimit` callbacks concurrently; `PrefetchCount` is the unacked-message window per channel).
A partitioned queue is forced to a single channel with ordered dispatch — parallelism then comes from the
partition lanes (one sequential lane per key), bounded by `PrefetchCount`.

### Transport options

Transport tuning binds from the `Messaging:RabbitMq` section and can be overridden in
code (code takes precedence). The publisher pools channels for concurrent publishing —
each leased channel awaits its own publisher confirm — so `PublishChannelPoolSize`
(default `10`) bounds how many publishes can be in flight at once.

```json
{
  "Messaging": {
    "RabbitMq": {
      "PublishChannelPoolSize": 32
    }
  }
}
```

```csharp
messaging.UseRabbitMq(configureTransport: options =>
{
    options.PublishChannelPoolSize = 32;
});
```

### Connection recovery

Connection and channel recovery is handled by `RabbitMQ.Client` (automatic + topology recovery, on by default
via the Aspire client), so a dropped connection re-establishes along with its queues, consumers, and the
request/reply listener's reply queue. A request in flight *during* a disconnect can still time out — its reply is
lost with the connection — and is retried by your own policy, not the transport.

### Tracing and health checks

`UseRabbitMq` registers an OpenTelemetry `ActivitySource`
(`"Vulthil.Messaging.RabbitMq"`) and a startup health check
(`"vulthil_messaging_rabbitmq_bus"`). Both registrations are gated on the Aspire
client's `DisableTracing` / `DisableHealthChecks` flags, so the toggles propagate
through to the Vulthil instrumentation. See
[Messaging — Observability](../messaging.md#observability) and
[Messaging — Health Checks](../messaging.md#health-checks).
