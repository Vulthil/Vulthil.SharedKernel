using Microsoft.EntityFrameworkCore;

namespace Vulthil.IntegrationTests.Fixtures;

/// <summary>
/// Reads index definitions from the PostgreSQL catalog, so tests assert what the database built rather than what the
/// EF model describes.
/// </summary>
internal static class NpgsqlIndexes
{
    /// <summary>
    /// Returns the predicate of the partial index <paramref name="indexName"/> as PostgreSQL prints it, or
    /// <see langword="null"/> when no such index exists or it has no predicate.
    /// </summary>
    public static async Task<string?> ReadPredicateAsync(DbContext context, string indexName, CancellationToken cancellationToken)
    {
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText =
                "SELECT pg_get_expr(i.indpred, i.indrelid) FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid WHERE c.relname = @indexName";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "indexName";
            parameter.Value = indexName;
            command.Parameters.Add(parameter);
            return await command.ExecuteScalarAsync(cancellationToken) as string;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
