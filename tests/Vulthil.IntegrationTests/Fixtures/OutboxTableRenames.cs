using Microsoft.EntityFrameworkCore;
using Vulthil.SharedKernel.Outbox;

namespace Vulthil.IntegrationTests.Fixtures;

/// <summary>
/// Renames the outbox table and every column to snake_case, simulating a consumer that applies a naming convention
/// or maps the outbox entity to custom identifiers. The provider stores' fetch SQL must keep working against such a
/// model, so these renames back the renamed-model relay tests.
/// </summary>
internal static class OutboxTableRenames
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<OutboxMessage>();
        entity.ToTable("outbox_messages");
        entity.Property(message => message.Id).HasColumnName("id");
        entity.Property(message => message.Type).HasColumnName("type");
        entity.Property(message => message.Content).HasColumnName("content");
        entity.Property(message => message.OccurredOnUtc).HasColumnName("occurred_on_utc");
        entity.Property(message => message.ProcessedOnUtc).HasColumnName("processed_on_utc");
        entity.Property(message => message.FailedOnUtc).HasColumnName("failed_on_utc");
        entity.Property(message => message.RetryCount).HasColumnName("retry_count");
        entity.Property(message => message.Error).HasColumnName("error");
        entity.Property(message => message.TraceParent).HasColumnName("trace_parent");
        entity.Property(message => message.TraceState).HasColumnName("trace_state");
        entity.Property(message => message.Destination).HasColumnName("destination");
        entity.Property(message => message.Metadata).HasColumnName("metadata");
    }
}
