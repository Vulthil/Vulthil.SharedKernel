using Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Infrastructure.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToContainer("Products");
        builder.HasNoDiscriminator();
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Id)
            .HasConversion(id => id.Value.ToString("N"), value => new ProductId(Guid.ParseExact(value, "N")));
        builder.HasPartitionKey(product => product.Id);
    }
}
