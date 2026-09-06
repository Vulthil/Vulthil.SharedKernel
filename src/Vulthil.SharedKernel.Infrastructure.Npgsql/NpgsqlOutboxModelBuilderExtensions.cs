using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Infrastructure.Npgsql.OutboxProcessing;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

namespace Vulthil.SharedKernel.Infrastructure.Npgsql;

/// <summary>
/// <see cref="ModelBuilder"/> extensions that apply the PostgreSQL-optimized <see cref="OutboxMessage"/> mapping.
/// </summary>
public static class NpgsqlOutboxModelBuilderExtensions
{
    /// <summary>
    /// Applies the provider-agnostic <see cref="OutboxMessage"/> mapping (<see cref="OutboxModelBuilderExtensions.ApplyOutbox"/>)
    /// and the PostgreSQL-optimized additions on top of it: the content column is stored as <c>jsonb</c> and a
    /// filtered partial index over <c>(OccurredOnUtc, Id)</c> serves the relay's pending-message query (rows that
    /// are neither processed nor dead-lettered).
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance, for chaining.</returns>
    public static ModelBuilder ApplyNpgsqlOutbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        return modelBuilder.ApplyOutbox().ApplyConfiguration(new OutboxMessageEntityConfiguration());
    }
}
