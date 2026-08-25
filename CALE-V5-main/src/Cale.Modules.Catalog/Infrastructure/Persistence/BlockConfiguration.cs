using Cale.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Catalog.Infrastructure.Persistence;

public sealed class BlockConfiguration : IEntityTypeConfiguration<Block>
{
    public void Configure(EntityTypeBuilder<Block> builder)
    {
        builder.ToTable("Bloques");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasColumnName("Nombre").IsRequired();
    }
}
