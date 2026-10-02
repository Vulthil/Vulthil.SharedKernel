# Vulthil.Extensions.Hosting

[![NuGet](https://img.shields.io/nuget/v/Vulthil.Extensions.Hosting)](https://www.nuget.org/packages/Vulthil.Extensions.Hosting)

Small hosting abstractions for Vulthil.

## `IRestartableHostedService`

A marker interface an [`IHostedService`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.ihostedservice)
implements to declare that its execution can be **stopped and started again cleanly**. Infrastructure can then pause
the service for the duration of an operation that must not run concurrently with it, and resume it afterwards.

The primary use is test isolation: `Vulthil.xUnit` pauses every registered hosted service implementing this marker
around the per-test database reset, so live background work — the `Vulthil.SharedKernel.Outbox` relay, the
`Vulthil.Messaging` consumers — does not run while the database is reset. A service opts in simply by implementing
the marker: no test-only code, and the testing library never depends on the outbox engine or the messaging library.

Implement it only on services whose `StartAsync`/`StopAsync` are idempotent across repeated cycles.

## `RestartableBackgroundService`

A restart-safe base class for such a service, used in place of `BackgroundService`. Every start runs `ExecuteAsync`
as a new generation. A stop cancels the generation and waits for it, and a start first waits for the previous
generation to finish, so two generations never run at the same time. A fault in a generation is logged and stops the
application, like a faulted `BackgroundService`.

```csharp
internal sealed class PollingWorker(IHostApplicationLifetime lifetime, ILogger<PollingWorker> logger)
    : RestartableBackgroundService(lifetime, logger)
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // One unit of work, then wait for the next poll.
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
```

Do not combine `BackgroundService` with the marker: the host observes only the execute task of a
`BackgroundService`'s first start and stops the whole host when that task is canceled while the application is
running — and on .NET 10 a stop racing service startup can cancel the task before `ExecuteAsync` has run at all.

## Docs

Usage patterns and articles: https://vulthil.github.io/Vulthil.SharedKernel/
