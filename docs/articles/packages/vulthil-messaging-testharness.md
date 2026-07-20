# Vulthil.Messaging.TestHarness

An in-memory messaging transport for tests. It runs your consumers with no broker and captures every produced
and consumed message for assertion. Built entirely on the public `Vulthil.Messaging.Transport` SDK, so it
assembles the same execution plans (consumers, polymorphic dispatch, per-consumer retry resolution) from your
queue configuration that a real transport would.

## When to use

- Component/unit tests that assert published, sent, consumed, or requested messages
- Integration tests that exercise the production composition without a broker
- Standing in for an external service (mock a request reply or a downstream event handler)

## Pattern

- Dispatch is synchronous: when a publish/send/request call returns, every consumer it triggered has run — no polling
- A one-way consumer's exception does **not** propagate to the publisher/sender: the consumer is retried per its
  resolved retry policy (attempts run back-to-back, without the configured delays), then a `Fault<T>` is published
  and captured — assert it via `Published<Fault<TMessage>>()`. A request consumer's exception becomes a failed
  `Result<TResponse>` on the requesting side
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

See the [Testing guide](../testing.md#messaging-test-harness) for the full API and details.
