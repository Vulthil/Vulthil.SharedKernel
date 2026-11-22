using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Orders;

namespace Orders.Infrastructure.Data.EntityConfigurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(order => order.Id);
        builder.Property(order => order.Id)
            .HasConversion(id => id.Value, value => new OrderId(value))
            .ValueGeneratedNever();

        builder.Property(order => order.Description).HasMaxLength(Order.MaxDescriptionLength).IsRequired();
    }
}
