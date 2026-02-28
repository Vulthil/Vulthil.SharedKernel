using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Application.Data;

namespace Vulthil.SharedKernel.Infrastructure.OutboxProcessing;

/// <summary>
/// Abstraction for provider-specific outbox lock acquisition, message fetching, updating, and transactional boundary.
/// The default implementation (<see cref="RelationalOutboxStrategy"/>) uses EF Core relational APIs.
/// Relational provider packages (e.g., Npgsql, SqlServer) should inherit from <see cref="RelationalOutboxStrategy"/>
/// to share update logic and override fetching with row-level locking.
/// Non-relational providers (e.g., Cosmos DB) should implement this interface directly.
/// </summary>
public interface IOutboxStrategy
{
    Task<IDbTransaction?> BeginTransactionAsync(ISaveOutboxMessages context, CancellationToken cancellationToken);
    Task<List<OutboxMessageData>> FetchMessagesAsync(DbSet<OutboxMessage> outboxMessages, int batchSize, int maxRetries, CancellationToken cancellationToken);
    Task UpdateMessagesAsync(DbSet<OutboxMessage> outboxMessages, IReadOnlyList<Guid> successIds, IReadOnlyList<OutboxMessageFailure> failures, int maxRetries, DateTimeOffset processedOnUtc, CancellationToken cancellationToken);
}

public readonly record struct OutboxMessageData(Guid Id, string Type, string Content);
public readonly record struct OutboxMessageFailure(Guid Id, string Error);
