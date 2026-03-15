using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vulthil.SharedKernel.Application.Messaging.DomainEvents;

namespace Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

internal sealed class OutboxProcessor(
    TimeProvider timeProvider,
    ISaveOutboxMessages outboxMessagesDbContext,
    IDomainEventPublisher domainEventPublisher,
    IOutboxStrategy outboxStrategy,
    IOptions<OutboxProcessingOptions> options,
    ILogger<OutboxProcessor> logger)
{
    private static readonly ConcurrentDictionary<string, Type> _typeCache = [];

    private OutboxProcessingOptions Options => options.Value;

    internal async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        var transaction = await outboxStrategy.BeginTransactionAsync(outboxMessagesDbContext, cancellationToken);

        try
        {
            var outboxMessages = await outboxStrategy.FetchMessagesAsync(
                outboxMessagesDbContext.OutboxMessages,
                Options.BatchSize,
                Options.MaxRetries,
                cancellationToken);

            if (outboxMessages.Count == 0)
            {
                return 0;
            }

            List<PublishResult> results;

            if (Options.EnableParallelPublishing)
            {
                var tasks = outboxMessages.Select(m => TryPublishAsync(m, cancellationToken));
                results = [.. await Task.WhenAll(tasks)];
            }
            else
            {
                results = new(outboxMessages.Count);
                foreach (var message in outboxMessages)
                {
                    results.Add(await TryPublishAsync(message, cancellationToken));
                }
            }

            var now = timeProvider.GetUtcNow();
            var successIds = results.Where(r => r.Success).Select(r => r.Id).ToList();
            var failures = results.Where(r => !r.Success)
                .Select(r => new OutboxMessageFailure(r.Id, r.Error!))
                .ToList();

            await outboxStrategy.UpdateMessagesAsync(
                outboxMessagesDbContext.OutboxMessages,
                successIds,
                failures,
                Options.MaxRetries,
                now,
                cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return outboxMessages.Count;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private async Task<PublishResult> TryPublishAsync(OutboxMessageData outboxMessage, CancellationToken cancellationToken)
    {
        Activity? activity = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(outboxMessage.TraceParent))
            {
                var parent = ActivityContext.Parse(outboxMessage.TraceParent, outboxMessage.TraceState);
                activity = Telemetry.ActivitySource.StartActivity("OutboxPublishing", ActivityKind.Producer, parent);
            }
            var messageType = GetOrAddMessageType(outboxMessage.Type);
            var message = JsonSerializer.Deserialize(outboxMessage.Content, messageType)!;

            await domainEventPublisher.PublishAsync(message, cancellationToken);

            return new PublishResult(outboxMessage.Id, Success: true);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to publish outbox message {MessageId}", outboxMessage.Id);
            return new PublishResult(outboxMessage.Id, Success: false, Error: exception.ToString());
        }
        finally
        {
            activity?.Dispose();
        }
    }

    private static Type GetOrAddMessageType(string typeName) => _typeCache.GetOrAdd(typeName, t =>
    {
        var type = Type.GetType(t);
        type ??= AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(t))
                .FirstOrDefault(t => t is not null);

        return type!;
    });

    private readonly record struct PublishResult(Guid Id, bool Success, string? Error = null);
}

/// <summary>
/// Static class for holding the Telemetry ActivitySource used for all outbox processing operations, enabling consistent correlation of telemetry across the capture, processing, and publishing stages.
/// </summary>
public static class Telemetry
{
    /// <summary>
    /// ActivitySourceName for all outbox processing operations, allowing correlation of events across the capture, processing, and publishing stages.
    /// </summary>
    public static readonly string ActivitySourceName = "Vulthil.SharedKernel.Infrastructure";
    /// <summary>
    /// ActivitySource for all outbox processing operations, allowing correlation of events across the capture, processing, and publishing stages.
    /// </summary>
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
