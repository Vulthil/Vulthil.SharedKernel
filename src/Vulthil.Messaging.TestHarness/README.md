# Vulthil.Messaging.TestHarness

[![NuGet](https://img.shields.io/nuget/v/Vulthil.Messaging.TestHarness)](https://www.nuget.org/packages/Vulthil.Messaging.TestHarness)

An in-memory messaging transport for tests: runs your consumers with no broker and captures produced and
consumed messages for assertion.

## Install

`dotnet add package Vulthil.Messaging.TestHarness`

## Quick start

```csharp
builder.AddMessaging(messaging =>
{
    messaging.ConfigureQueue("orders", q => q.AddConsumer<OrderCreatedConsumer>());
    messaging.UseTestHarness(); // in place of a broker transport
});

var harness = host.Services.GetRequiredService<ITestHarness>();
harness.Published<OrderCreatedEvent>().ShouldHaveSingleItem();
harness.Consumed<OrderCreatedEvent>().ShouldHaveSingleItem();
```

For an integration test that keeps the production composition, swap the transport instead:
`services.ReplaceTransportWithTestHarness()`.

## Docs

Usage patterns: https://github.com/Vulthil/Vulthil.SharedKernel/tree/main/docs/articles/packages
