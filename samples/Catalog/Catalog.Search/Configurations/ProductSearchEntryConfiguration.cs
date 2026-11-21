using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Catalog.Search.Configurations;

public sealed class ProductSearchEntryConfiguration : IEntityTypeConfiguration<ProductSearchEntry>
{
    public void Configure(EntityTypeBuilder<ProductSearchEntry> builder)
    {
        builder.ToContainer("ProductSearch");
        builder.HasNoDiscriminator();
        builder.HasKey(entry => entry.Id);
        builder.HasPartitionKey(entry => entry.Id);
    }
}
