using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shipments.Domain;

namespace Shipments.Infrastructure.Configurations;

public sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.HasKey(shipment => shipment.Id);
        builder.Property(shipment => shipment.Id)
            .HasConversion(id => id.Value, value => new ShipmentId(value))
            .ValueGeneratedNever();

        builder.Property(shipment => shipment.Recipient).HasMaxLength(200).IsRequired();
        builder.Property(shipment => shipment.Carrier).HasMaxLength(100).IsRequired();
        builder.Property(shipment => shipment.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(shipment => shipment.OrderReference);
        builder.HasIndex(shipment => new { shipment.Status, shipment.DeliveredAtUtc });

        builder.HasMany(shipment => shipment.TrackingEvents)
            .WithOne()
            .HasForeignKey("ShipmentId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(shipment => shipment.TrackingEvents).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
