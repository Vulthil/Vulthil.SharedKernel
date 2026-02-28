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
        CancellationToken cancellationToken)
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
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new OrderDto());
    }
}
```

## Registering Queues and Consumers

Registration happens in the composition root using the `AddMessaging` builder:

```csharp
builder.AddMessaging(messaging =>
{
    messaging.UseRabbitMq();

    messaging.AddQueue("order-events", queue =>
    {
        queue.AddConsumer<OrderCreatedConsumer>();
        queue.UseRetry(retry =>
        {
            retry.Exponential(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
            retry.UseJitter(0.2);
        });
        queue.UseDeadLetterQueue();
    });

    messaging.AddQueue("order-requests", queue =>
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
        await publisher.PublishAsync(new OrderCreatedEvent(order.Id), ct);
    }
}
```

## Routing Keys

Routing keys control which consumers receive a message on topic exchanges.

### Attribute-based routing

```csharp
[RoutingKey("order.created")]
public sealed class OrderCreatedConsumer : IConsumer<OrderCreatedEvent> { ... }
```

### Dynamic routing keys

```csharp
messaging.RegisterRoutingKeyFormatter<OrderCreatedEvent>(e => $"order.{e.Region}");
messaging.RegisterCorrelationIdFormatter<OrderCreatedEvent>(e => e.OrderId.ToString());
```

## Queue Configuration

Queue settings can be tuned in code or bound from `appsettings.json`:

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

```csharp
queue.ConfigureQueue(q =>
{
    q.PrefetchCount = 32;
    q.ExchangeType = MessagingExchangeType.Topic;
});
```

## Testing Messaging

`Vulthil.Messaging.TestHarness` provides an in-memory transport that captures published messages for assertion:

```csharp
var published = testHarness.Published<OrderCreatedEvent>();
Assert.Single(published);
Assert.Equal(expectedOrderId, published.First().Message.OrderId);
```

See [Testing](testing.md) for more details on integration test setup.
