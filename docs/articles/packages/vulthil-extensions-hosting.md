# Vulthil.Extensions.Hosting

Small hosting abstractions for Vulthil: the `IRestartableHostedService` marker and
`RestartableBackgroundService`, a restart-safe base class for a service that carries it.

## When to use

- A hosted service (such as a database-polling relay) should be pausable around operations that must not run
  concurrently with it
- Test infrastructure needs to stop background work around a reset and resume it afterwards

## `IRestartableHostedService`

A marker interface an `IHostedService` implements to declare that its execution can be **stopped and started
again cleanly**. Infrastructure can then pause the service for the duration of an operation that must not run
concurrently with it, and resume it afterwards.

The primary use is test isolation: `Vulthil.xUnit` stops every registered hosted service implementing this
marker around the per-test database reset and restarts it afterwards, so live background work does not run while
the database is reset. Two services in this repository opt in: the `Vulthil.SharedKernel.Outbox` relay and the
`Vulthil.Messaging` consumer host (which stops and restarts the transport's consumers). They opt in simply by
implementing the marker — no test-only code, and the testing library never depends on the outbox engine or the
messaging library.

## `RestartableBackgroundService`

A restart-safe base class for a long-running service with the marker, used in place of `BackgroundService`:

- Every start runs `ExecuteAsync` as a new **generation** with its own stopping token.
- A stop cancels the running generation and waits for it, bounded by the caller's token.
- A start first waits for the previous generation to finish, so two generations never run at the same time —
  even when a stop gave up waiting early. Of two overlapping starts, only one begins a generation.
- The execute task (`ExecuteTask`) always completes successfully. A cancellation after a stop ends it gracefully;
  any other exception is logged and stops the application, like a faulted `BackgroundService`.
- `Dispose` cancels the running generation; a later start does nothing.

A generation releases everything it acquired before it returns, because the next generation starts only after it
has finished. The messaging consumer host relies on this: each generation starts the transport and stops it again.

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

## Implementation guidance

Implement the marker only on services whose `StartAsync`/`StopAsync` are idempotent across repeated cycles; deriving
from `RestartableBackgroundService` gives you that. Do not combine `BackgroundService` with the marker: the host
observes only the execute task of a `BackgroundService`'s first start and stops the whole host when that task is
canceled while the application is running — and on .NET 10 a stop racing service startup can cancel the task before
`ExecuteAsync` has run at all.

See [Testing](../testing.md) for how the marker participates in the per-test reset.
