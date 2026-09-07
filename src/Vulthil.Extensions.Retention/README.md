# Vulthil.Extensions.Retention

[![NuGet](https://img.shields.io/nuget/v/Vulthil.Extensions.Retention)](https://www.nuget.org/packages/Vulthil.Extensions.Retention)

One retention sweep for any store that can delete entries older than a cutoff.

## Install

`dotnet add package Vulthil.Extensions.Retention`

## Usage

`AddRetentionSweep<TStore>` registers a hosted service that runs on host start and then once per sweep interval. Each
sweep resolves `TStore` from a fresh scope, asks your adapter for its delete operation, and deletes entries older than
the retention period in bounded batches until fewer than a full batch remain.

```csharp
services.AddRetentionSweep<IOutboxStore>(
    "Outbox",
    provider => new RetentionSweepSettings(TimeSpan.FromDays(7), TimeSpan.FromHours(1), batchSize: 1000),
    store => store is IOutboxRetentionStore retention ? new RetentionSweepDeleter(retention.DeleteProcessedAsync) : null);
```

A store for which the adapter returns `null` is logged once and never swept. Every other failure is logged and retried
on the next interval. `Vulthil.SharedKernel.Outbox` and `Vulthil.Messaging.Inbox` run their retention through this
package.

## Docs

Usage patterns and articles: https://vulthil.github.io/Vulthil.SharedKernel/
