using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApi.Models;

namespace WebApi.Data.EntityConfigurations;

public sealed class WebApiEntityConfiguration : IEntityTypeConfiguration<WebApiEntity>
{
    public void Configure(EntityTypeBuilder<WebApiEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired();

        builder.Property(e => e.Id)
            .HasConversion(
                v => v.Value,
                v => new WebApiEntityId(v));
    }
}
