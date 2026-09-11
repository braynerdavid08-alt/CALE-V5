using Cale.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Catalog.Infrastructure.Persistence;

public sealed class CatalogMediaConfiguration : IEntityTypeConfiguration<CatalogMediaBlob>
{
    public void Configure(EntityTypeBuilder<CatalogMediaBlob> builder)
    {
        builder.ToTable("CatalogMediaBlobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Data).IsRequired();
        builder.HasIndex(x => x.OwnerId);
        builder.HasIndex(x => x.CreatedAt);
    }
}
