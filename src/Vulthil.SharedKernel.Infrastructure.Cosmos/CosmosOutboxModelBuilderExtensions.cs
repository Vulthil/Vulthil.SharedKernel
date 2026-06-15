using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Infrastructure.Cosmos.OutboxProcessing;
using Vulthil.SharedKernel.Outbox;

namespace Vulthil.SharedKernel.Infrastructure.Cosmos;

/// <summary>
/// <see cref="ModelBuilder"/> extensions that apply the Cosmos DB <see cref="OutboxMessage"/> mapping.
/// </summary>
public static class CosmosOutboxModelBuilderExtensions
{
    /// <summary>
    /// Applies the Cosmos DB <see cref="OutboxMessage"/> entity configuration. Consumers targeting Cosmos DB can
    /// further customize container and partition settings in their own <see cref="DbContext"/>.
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance, for chaining.</returns>
    public static ModelBuilder ApplyCosmosOutbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        return modelBuilder.ApplyConfiguration(new OutboxMessageEntityConfiguration());
    }
}
