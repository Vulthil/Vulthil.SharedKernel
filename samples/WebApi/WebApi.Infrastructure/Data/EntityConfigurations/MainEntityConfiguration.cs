using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApi.Domain.MainEntities;

namespace WebApi.Infrastructure.Data.EntityConfigurations;

public sealed class MainEntityConfiguration : IEntityTypeConfiguration<MainEntity>
{
    public void Configure(EntityTypeBuilder<MainEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).IsRequired();

        builder.Property(e => e.Id)
            .HasConversion(
                v => v.Value,
                v => new MainEntityId(v));
    }
}
