using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shipments.Domain;

namespace Shipments.Infrastructure.Configurations;

public sealed class TrackingEventConfiguration : IEntityTypeConfiguration<TrackingEvent>
{
    public void Configure(EntityTypeBuilder<TrackingEvent> builder)
    {
        builder.ToTable("TrackingEvents");
        builder.HasKey(trackingEvent => trackingEvent.Id);

        // The id is assigned in the domain, so a tracking event added to a loaded shipment must be inserted, not
        // treated as an existing row that EF Core would update.
        builder.Property(trackingEvent => trackingEvent.Id)
            .HasConversion(id => id.Value, value => new TrackingEventId(value))
            .ValueGeneratedNever();

        builder.Property(trackingEvent => trackingEvent.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(trackingEvent => trackingEvent.Location).HasMaxLength(200).IsRequired();
    }
}
