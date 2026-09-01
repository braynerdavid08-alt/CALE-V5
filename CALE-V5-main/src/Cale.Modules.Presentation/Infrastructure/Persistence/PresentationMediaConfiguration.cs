using Cale.Modules.Presentation.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Presentation.Infrastructure.Persistence;

public sealed class PresentationMediaConfiguration : IEntityTypeConfiguration<PresentationMediaBlob>
{
    public void Configure(EntityTypeBuilder<PresentationMediaBlob> builder)
    {
        builder.ToTable("PresentationMediaBlobs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Data).IsRequired();
        builder.HasIndex(x => x.OwnerId);
        builder.HasIndex(x => x.CreatedAt);
    }
}
