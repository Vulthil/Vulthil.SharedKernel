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
