# Vulthil.Messaging.TestHarness

Use `Vulthil.Messaging.TestHarness` to validate messaging behavior in tests.

## When to use

- Integration tests that assert published/consumed messages
- End-to-end verification of messaging flows

## Pattern

- Treat message assertions as behavior verification
- Keep test setup explicit and deterministic
- Isolate external broker dependencies when possible

## Usage

### Verifying published messages

```csharp
// Replace the real publisher with the test harness in your WebApplicationFactory,
// then assert that expected messages were published after an action:
var published = testHarness.Published<OrderCreatedEvent>();
Assert.Single(published);
Assert.Equal(expectedOrderId, published.First().Message.OrderId);
```
