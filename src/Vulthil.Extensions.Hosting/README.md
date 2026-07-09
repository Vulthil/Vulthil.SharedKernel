# Vulthil.Extensions.Hosting

[![NuGet](https://img.shields.io/nuget/v/Vulthil.Extensions.Hosting)](https://www.nuget.org/packages/Vulthil.Extensions.Hosting)

Small hosting abstractions for Vulthil.

## `IRestartableHostedService`

A marker interface an [`IHostedService`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.ihostedservice)
implements to declare that its execution can be **stopped and started again cleanly**. Infrastructure can then pause
the service for the duration of an operation that must not run concurrently with it, and resume it afterwards.

The primary use is test isolation: `Vulthil.xUnit` pauses every registered hosted service implementing this marker
around the per-test database reset, so a live database-polling relay (for example the
`Vulthil.SharedKernel.Outbox` background relay) does not contend with the reset and time it out. The relay opts in
simply by implementing the marker — no test-only code, and the testing library never depends on the outbox engine.

```csharp
internal sealed class OutboxBackgroundService(/* ... */) : IRestartableHostedService, IDisposable
{
    // StartAsync/StopAsync manage the execute task so a Stop/Start cycle re-runs it cleanly.
}
```

Implement it only on services whose `StartAsync`/`StopAsync` are idempotent across repeated cycles. Prefer
implementing `IHostedService` directly over inheriting `BackgroundService`: the host observes only the execute task
of a `BackgroundService`'s first start and stops the whole host when that task is canceled while the application is
running — and on .NET 10 a stop racing service startup can cancel the task before `ExecuteAsync` has run at all.
