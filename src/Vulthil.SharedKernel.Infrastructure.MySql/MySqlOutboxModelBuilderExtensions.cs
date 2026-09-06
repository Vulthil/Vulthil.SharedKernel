using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Infrastructure.MySql.OutboxProcessing;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

namespace Vulthil.SharedKernel.Infrastructure.MySql;

/// <summary>
/// <see cref="ModelBuilder"/> extensions that apply the MySQL-optimized <see cref="OutboxMessage"/> mapping.
/// </summary>
public static class MySqlOutboxModelBuilderExtensions
{
    /// <summary>
    /// Applies the provider-agnostic <see cref="OutboxMessage"/> mapping (<see cref="OutboxModelBuilderExtensions.ApplyOutbox"/>)
    /// and the MySQL-optimized additions on top of it: a bounded type column, <c>longtext</c> content, and a
    /// composite index over the relay's pending-state and ordering columns. MySQL has no filtered (partial) indexes,
    /// so the index is unfiltered.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance, for chaining.</returns>
    public static ModelBuilder ApplyMySqlOutbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        return modelBuilder.ApplyOutbox().ApplyConfiguration(new OutboxMessageEntityConfiguration());
    }
}
