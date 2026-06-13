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

### Tracing and health checks

`UseRabbitMq` registers an OpenTelemetry `ActivitySource`
(`"Vulthil.Messaging.RabbitMq"`) and a startup health check
(`"vulthil_messaging_rabbitmq_bus"`). Both registrations are gated on the Aspire
client's `DisableTracing` / `DisableHealthChecks` flags, so the toggles propagate
through to the Vulthil instrumentation. See
[Messaging — Observability](../messaging.md#observability) and
[Messaging — Health Checks](../messaging.md#health-checks).
