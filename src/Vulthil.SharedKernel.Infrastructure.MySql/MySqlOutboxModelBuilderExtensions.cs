using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Infrastructure.MySql.OutboxProcessing;
using Vulthil.SharedKernel.Outbox;

namespace Vulthil.SharedKernel.Infrastructure.MySql;

/// <summary>
/// <see cref="ModelBuilder"/> extensions that apply the MySQL-optimized <see cref="OutboxMessage"/> mapping.
/// </summary>
public static class MySqlOutboxModelBuilderExtensions
{
    /// <summary>
    /// Applies the MySQL-optimized <see cref="OutboxMessage"/> entity configuration: the content column is stored
    /// as <c>longtext</c> with a composite index over the relay's pending-state and ordering columns. MySQL has no
    /// filtered (partial) indexes, so the index is unfiltered.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance, for chaining.</returns>
    public static ModelBuilder ApplyMySqlOutbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        return modelBuilder.ApplyConfiguration(new OutboxMessageEntityConfiguration());
    }
}
