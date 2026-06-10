using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Application.Data;

namespace Vulthil.SharedKernel.Outbox;

/// <summary>
/// Abstraction for provider-specific outbox lock acquisition, message fetching, updating, and transactional boundary.
/// </summary>
public interface IOutboxStrategy
{
    /// <summary>
    /// Begins a transaction for the outbox processing cycle, if the context supports transactions.
    /// </summary>
    /// <param name="context">The outbox persistence context.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A transaction to commit on success, or <see langword="null"/> if transactions are not supported.</returns>
    Task<IDbTransaction?> BeginTransactionAsync(ISaveOutboxMessages context, CancellationToken cancellationToken);
    /// <summary>
    /// Fetches unprocessed outbox messages for delivery.
    /// </summary>
    /// <param name="outboxMessages">The outbox message set.</param>
    /// <param name="batchSize">The maximum number of messages to fetch.</param>
    /// <param name="maxRetries">The maximum retry count; messages at or above this count are excluded.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A list of message data ready for publishing.</returns>
    Task<List<OutboxMessageData>> FetchMessagesAsync(DbSet<OutboxMessage> outboxMessages, int batchSize, int maxRetries, CancellationToken cancellationToken);
    /// <summary>
    /// Marks processed messages as complete and records failures with retry counts.
    /// </summary>
    /// <param name="context">The outbox message context.</param>
    /// <param name="successIds">Identifiers of successfully published messages.</param>
    /// <param name="failures">Details of messages that failed to publish.</param>
    /// <param name="maxRetries">The maximum retry count; messages reaching this limit are also marked as processed.</param>
    /// <param name="processedOnUtc">The UTC timestamp to record for processed messages.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task UpdateMessagesAsync(ISaveOutboxMessages context, IReadOnlyList<Guid> successIds, IReadOnlyList<OutboxMessageFailure> failures, int maxRetries, DateTimeOffset processedOnUtc, CancellationToken cancellationToken);
}

/// <summary>
/// Contains the data of an outbox message fetched for delivery.
/// </summary>
/// <param name="Id">The message identifier.</param>
/// <param name="Type">The fully-qualified type name of the message.</param>
/// <param name="Content">The JSON-serialized message content.</param>
/// <param name="TraceParent">Distributed TraceParent</param>
/// <param name="TraceState">Distributed TraceState</param>
/// <param name="Destination">The sink this message is relayed to, used to select the dispatcher.</param>
/// <param name="Metadata">Optional destination-specific metadata (JSON), or <see langword="null"/>.</param>
public readonly record struct OutboxMessageData(Guid Id, string Type, string Content, string? TraceParent, string? TraceState, OutboxDestination Destination, string? Metadata);
/// <summary>
/// Contains failure details for an outbox message that could not be published.
/// </summary>
/// <param name="Id">The message identifier.</param>
/// <param name="Error">The error message describing the failure.</param>
public readonly record struct OutboxMessageFailure(Guid Id, string Error);
