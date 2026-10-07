using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Vulthil.SharedKernel.Outbox;

namespace Vulthil.SharedKernel.Infrastructure.Npgsql.OutboxProcessing;

/// <summary>
/// Writes the filter of the PostgreSQL pending-message index (rows neither processed nor dead-lettered) over the
/// mapped <c>ProcessedOnUtc</c> and <c>FailedOnUtc</c> column names. <see cref="OutboxMessageEntityConfiguration"/>
/// writes it from the names mapped when <c>ApplyNpgsqlOutbox</c> runs, which already include a naming convention's
/// renames; at model finalizing this convention writes it again from the final names, so <c>HasColumnName</c>
/// renames made after that call are honored too. Only a filter that the mapping wrote is rewritten: a filter the
/// application sets on the index, or an index over the same columns that the application adds without a filter, is
/// left as it is.
/// </summary>
internal sealed class PendingIndexFilterConvention : IModelFinalizingConvention
{
    /// <summary>
    /// Writes the filter of <paramref name="index"/> from the column names its entity type maps now. The filter is
    /// written with convention precedence, so a filter the application sets on the index wins and the finalizing pass
    /// may still replace this one.
    /// </summary>
    /// <param name="index">The pending-message index of the <see cref="OutboxMessage"/> entity type.</param>
    public static void Apply(IMutableIndex index)
    {
        var conventionIndex = (IConventionIndex)index;
        if (Filter(conventionIndex.DeclaringEntityType) is { } filter)
        {
            conventionIndex.Builder.HasFilter(filter);
        }
    }

    /// <inheritdoc />
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        if (modelBuilder.Metadata.FindEntityType(typeof(OutboxMessage)) is not { } entityType
            || FindPendingIndex(entityType) is not { } index
            || index.GetFilterConfigurationSource() != ConfigurationSource.Convention
            || Filter(entityType) is not { } filter)
        {
            return;
        }

        index.Builder.HasFilter(filter);
    }

    private static IConventionIndex? FindPendingIndex(IConventionEntityType entityType) =>
        entityType.FindProperty(nameof(OutboxMessage.OccurredOnUtc)) is { } occurredOnUtc
        && entityType.FindProperty(nameof(OutboxMessage.Id)) is { } id
            ? entityType.FindIndex([occurredOnUtc, id])
            : null;

    private static string? Filter(IReadOnlyEntityType entityType)
    {
        if (StoreObjectIdentifier.Create(entityType, StoreObjectType.Table) is not { } table
            || entityType.FindProperty(nameof(OutboxMessage.ProcessedOnUtc))?.GetColumnName(table) is not { } processedOnUtc
            || entityType.FindProperty(nameof(OutboxMessage.FailedOnUtc))?.GetColumnName(table) is not { } failedOnUtc)
        {
            return null;
        }

        return $"{Quote(processedOnUtc)} IS NULL AND {Quote(failedOnUtc)} IS NULL";
    }

    // Quoting every identifier, not only those PostgreSQL would fold to lower case, keeps the default-name filter text
    // unchanged and needs no provider service, which the mapping-time write has no access to.
    private static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
