using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Outbox;

namespace Vulthil.SharedKernel.Outbox.EntityFrameworkCore;

/// <summary>
/// <see cref="ModelBuilder"/> extensions that apply the provider-agnostic <see cref="OutboxMessage"/> mapping.
/// </summary>
public static class OutboxModelBuilderExtensions
{
    /// <summary>
    /// Applies the provider-agnostic <see cref="OutboxMessage"/> entity configuration (primary key and required
    /// columns, without provider-specific column types or indexes). Provider packages expose optimized
    /// alternatives (for example <c>ApplyNpgsqlOutbox</c>).
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure.</param>
    /// <returns>The same <see cref="ModelBuilder"/> instance, for chaining.</returns>
    public static ModelBuilder ApplyOutbox(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        return modelBuilder.ApplyConfiguration(new OutboxMessageEntityConfiguration());
    }
}
