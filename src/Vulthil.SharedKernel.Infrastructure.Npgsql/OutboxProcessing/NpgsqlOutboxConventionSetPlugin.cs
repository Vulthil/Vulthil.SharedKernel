using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace Vulthil.SharedKernel.Infrastructure.Npgsql.OutboxProcessing;

/// <summary>
/// Adds <see cref="PendingIndexFilterConvention"/> to the conventions EF Core builds the model with.
/// </summary>
internal sealed class NpgsqlOutboxConventionSetPlugin : IConventionSetPlugin
{
    /// <inheritdoc />
    public ConventionSet ModifyConventions(ConventionSet conventionSet)
    {
        conventionSet.Add(new PendingIndexFilterConvention());
        return conventionSet;
    }
}
