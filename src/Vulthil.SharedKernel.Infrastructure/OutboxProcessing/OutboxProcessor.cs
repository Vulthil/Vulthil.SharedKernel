using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vulthil.SharedKernel.Application.Data;
using Vulthil.SharedKernel.Application.Messaging.DomainEvents;

namespace Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

internal class OutboxProcessor(
    TimeProvider timeProvider,
    ISaveOutboxMessages outboxMessagesDbContext,
    IDomainEventPublisher domainEventPublisher,
    IOptions<OutboxProcessingOptions> options)
{
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ISaveOutboxMessages _outboxMessagesDbContext = outboxMessagesDbContext;
    private readonly IDomainEventPublisher _domainEventPublisher = domainEventPublisher;
    private readonly IOptions<OutboxProcessingOptions> _options = options;

    private static readonly ConcurrentDictionary<string, Type> _typeCache = [];

    private int BatchSize => _options.Value.BatchSize;

    internal async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        IDbTransaction? transaction = null;
        if (_outboxMessagesDbContext is IUnitOfWork unitOfWorkContext)
        {
            transaction = await unitOfWorkContext.BeginTransactionAsync(cancellationToken);
        }

        var outboxMessages = await _outboxMessagesDbContext.OutboxMessages
            .Where(o => o.ProcessedOnUtc == null)
            .OrderBy(o => o.OccurredOnUtc)
            .Take(BatchSize)
            .Select(x => new OutboxMessageStruct(x.Id, x.Type, x.Content))
            .ToListAsync(cancellationToken);

        var updateQueue = new ConcurrentQueue<OutboxUpdate>();

        var publishTasks = outboxMessages.
            Select(m => PublishMessage(m, updateQueue, _timeProvider, _domainEventPublisher, cancellationToken))
            .ToList();

        await Task.WhenAll(publishTasks);

        foreach (var outboxUpdate in updateQueue)
        {

            await _outboxMessagesDbContext.OutboxMessages
                .Where(x => x.Id == outboxUpdate.Id)
                .ExecuteUpdateAsync(
                setter => setter
                    .SetProperty(o => o.ProcessedOnUtc, outboxUpdate.ProcessedOnUtc)
                    .SetProperty(m => m.Error, outboxUpdate.Error),
                cancellationToken);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static async Task PublishMessage(OutboxMessageStruct outboxMessage, ConcurrentQueue<OutboxUpdate> updateQueue, TimeProvider timeProvider, IDomainEventPublisher domainEventPublisher, CancellationToken stoppingToken)
    {
        try
        {
            var messageType = GetOrAddMessageType(outboxMessage.Type);
            var message = JsonSerializer.Deserialize(outboxMessage.Content, messageType)!;

            await domainEventPublisher.PublishAsync(message, stoppingToken);

            updateQueue.Enqueue(new OutboxUpdate(outboxMessage.Id, timeProvider.GetUtcNow()));
        }
        catch (Exception exception)
        {
            updateQueue.Enqueue(new OutboxUpdate(outboxMessage.Id, timeProvider.GetUtcNow(), exception.ToString()));
        }
    }

    private static Type GetOrAddMessageType(string typeName) => _typeCache.GetOrAdd(typeName, t => Type.GetType(t)!);

    private readonly record struct OutboxMessageStruct(Guid Id, string Type, string Content);
    private readonly record struct OutboxUpdate(Guid Id, DateTimeOffset ProcessedOnUtc, string? Error = null);
}
