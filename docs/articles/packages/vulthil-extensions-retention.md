# Vulthil.Extensions.Retention

One retention sweep for any store that can delete entries older than a cutoff. The package contains one entry point,
`AddRetentionSweep<TStore>`, and the two small types it takes: `RetentionSweepSettings` and `RetentionSweepDeleter`.

## When to use

- A store accumulates terminal entries (processed rows, idempotency markers, audit records) that should be pruned
  after a retention period without a manual job
- You are writing a store for `Vulthil.SharedKernel.Outbox` or `Vulthil.Messaging.Inbox` and want the same sweep the
  first-party stores get — both packages already register their retention through this one

## Pattern

- Register one sweep per store type with `AddRetentionSweep<TStore>(name, settings, deleter)`; a second call for the
  same `TStore` is a no-op
- Keep the on/off decision and the validation of the retention values on your own options, and only register the
  sweep when it is enabled — the sweep itself has no `Enabled` flag
- Let the `deleter` adapter return `null` for a store that cannot delete old entries; the sweep logs that once and
  stays idle instead of failing

## Usage

```csharp
services.AddRetentionSweep<IOutboxStore>(
    "Outbox",
    provider =>
    {
        var retention = provider.GetRequiredService<IOptions<OutboxProcessingOptions>>().Value.Retention;
        return new RetentionSweepSettings(retention.RetentionPeriod, retention.SweepInterval, retention.BatchSize);
    },
    store => store is IOutboxRetentionStore retention
        ? new RetentionSweepDeleter(retention.DeleteProcessedAsync)
        : null);
```

The hosted service runs a sweep on host start and then once per `SweepInterval`. Each sweep resolves `TStore` from a
fresh scope, asks the adapter for the store's `RetentionSweepDeleter`, and calls it with the cutoff
(`now - RetentionPeriod`) and `BatchSize` repeatedly until a call deletes fewer than a full batch — so a large backlog
drains in bounded, short-lived deletes. The `settings` callback runs once, when the hosted service is created, so
values bound from configuration or post-configured are honoured.

## Failure handling

Every failure inside a sweep is logged (`"{Name} retention sweep failed."`) and the sweep is retried on the next
interval. A cancellation that does not come from the host stopping — for example a store client surfacing a timeout as
an `OperationCanceledException` — is treated the same way, so a misbehaving store can never bring the host down
through the sweep. Only the host's own stop ends the loop.
