# Vulthil.Messaging.TestHarness

An in-memory messaging transport for tests. It runs your consumers with no broker and captures every produced
and consumed message for assertion. Built entirely on the public `Vulthil.Messaging.Transport` SDK, so it
mirrors the real consumer topology assembled from your queue configuration.

## When to use

- Component/unit tests that assert published, sent, consumed, or requested messages
- Integration tests that exercise the production composition without a broker
- Standing in for an external service (mock a request reply or a downstream event handler)

## Pattern

- Dispatch is synchronous: when a publish/send/request call returns, every consumer it triggered has run — no polling
- A one-way consumer's exception propagates to the publisher/sender; a request consumer's exception becomes a failed result
- Keep assertions on `ITestHarness` deterministic and explicit; `Clear()` between phases

## Usage

### Compose the harness (unit/component tests)

```csharp
var builder = Host.CreateApplicationBuilder();
builder.AddMessaging(messaging =>
{
    messaging.ConfigureQueue("orders", q => q.AddConsumer<OrderCreatedConsumer>());
    messaging.UseTestHarness();
});
using var host = builder.Build();

var publisher = host.Services.GetRequiredService<IPublisher>();
var harness = host.Services.GetRequiredService<ITestHarness>();

await publisher.PublishAsync(new OrderCreatedEvent(orderId));

harness.Published<OrderCreatedEvent>().ShouldHaveSingleItem().Message.OrderId.ShouldBe(orderId);
harness.Consumed<OrderCreatedEvent>().ShouldHaveSingleItem();
```

### Mock responses

```csharp
// Answer a request as an external service would (takes precedence over a real request consumer):
harness.Respond<GetWeatherRequest, WeatherForecast>(ctx => new WeatherForecast(ctx.Message.City, 20));

// React to a published/sent message as a fake downstream service:
harness.Handle<OrderShippedEvent>(ctx => { observed.Add(ctx.Message.OrderId); return Task.CompletedTask; });
```

### Swap the transport in integration tests

Call `ReplaceTransportWithTestHarness()` from a test host's service hook to replace the registered broker
transport with the harness, leaving production code untouched:

```csharp
builder.ConfigureServices(services => services.ReplaceTransportWithTestHarness());
```

See the [Testing guide](https://github.com/Vulthil/Vulthil.SharedKernel/tree/main/docs/articles/testing.md) for
the full API and details.
