namespace Vulthil.SharedKernel.Infrastructure.Relational;

/// <summary>
/// Shared helpers for relational provider packages.
/// </summary>
internal static class RelationalHelpers
{
    /// <summary>
    /// Returns the fully-qualified OutboxMessages table name for raw SQL queries. Uses literal quoting.
    /// </summary>
    internal static string OutboxTableName => "OutboxMessages";
}
