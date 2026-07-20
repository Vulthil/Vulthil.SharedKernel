# Vulthil.Messaging

Use `Vulthil.Messaging` to configure messaging pipelines and hosted consumers.

## When to use

- Service registration for consumers and queues
- Runtime message handling orchestration

## Pattern

- Keep wiring in composition root
- Separate message contracts from processing logic
- Centralize retry/error strategy decisions

## Usage

### Registering queues and consumers

```csharp
builder.AddMessaging(messaging =>
{
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

### Routing key configuration

```csharp
messaging.ConfigureMessage<OrderCreatedEvent>(message =>
{
    message.UseRoutingKey(e => $"order.{e.Region}");
    message.UseCorrelationId(e => e.OrderId.ToString());
});
```

### Per-consumer retry override

`AddConsumer` accepts a configurator; its knob is the consumer's retry policy, which overrides the queue-level
default for that consumer alone:

```csharp
queue.AddConsumer<OrderCreatedConsumer>(c =>
{
    c.UseRetry(r => r.Immediate(5));
});
```

Binding patterns are configured per queue, not per consumer — use
`queue.Subscribe<OrderCreatedEvent>("order.eu")` (see [Messaging — Routing Keys](../messaging.md#routing-keys)).

### Configuration-driven setup

Queue and message settings under `Messaging:Queues:*` and `Messaging:Messages:*`
are loaded from `IConfiguration` before the configurator action runs, so a service
can be configured entirely via `appsettings.json`. Code calls merge on top of the
loaded values, with code winning on conflict. See
[Messaging — Configuration-driven Setup](../messaging.md#configuration-driven-setup).
