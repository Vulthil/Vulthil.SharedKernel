# Vulthil.Messaging.Abstractions

Use `Vulthil.Messaging.Abstractions` as the stable contract package.

## When to use

- Sharing consumer/publisher contracts across projects
- Defining request/reply contracts independently of transport

## Pattern

- Keep only interfaces/contracts in this package
- Avoid transport-specific types here
- Version contracts carefully for compatibility

## Usage

### Defining a consumer

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

### Defining a request/reply consumer

```csharp
public sealed class GetOrderConsumer : IRequestConsumer<GetOrderRequest, OrderDto>
{
    public Task<OrderDto> ConsumeAsync(
        IMessageContext<GetOrderRequest> messageContext,
        CancellationToken cancellationToken)
    {
        // Fetch and return the order
        return Task.FromResult(new OrderDto());
    }
}
```

### Publishing messages

```csharp
await publisher.PublishAsync(new OrderCreatedEvent(orderId), cancellationToken: ct);
```

### Request/reply

```csharp
Result<OrderDto> result = await requester.RequestAsync<GetOrderRequest, OrderDto>(
    new GetOrderRequest(orderId), cancellationToken: ct);
```
