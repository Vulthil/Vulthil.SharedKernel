using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Outbox;
using Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

namespace Vulthil.SharedKernel.Infrastructure.Cosmos;

/// <summary>
/// <see cref="ModelBuilder"/> extensions that apply the Cosmos DB <see cref="OutboxMessage"/> mapping.
/// </summary>
public static class CosmosOutboxModelBuilderExtensions
{
    /// <summary>
    /// Applies the <see cref="OutboxMessage"/> mapping for Cosmos DB. Cosmos DB needs no provider-specific column
    /// types or indexes, so this is the provider-agnostic mapping
    /// (<see cref="OutboxModelBuilderExtensions.ApplyOutbox"/>); consumers can further customize container and
    /// partition settings in their own <see cref="DbContext"/>.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance, for chaining.</returns>
    public static ModelBuilder ApplyCosmosOutbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        return modelBuilder.ApplyOutbox();
    }
}
