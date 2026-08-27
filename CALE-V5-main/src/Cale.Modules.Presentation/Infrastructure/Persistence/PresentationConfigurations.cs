using Cale.Modules.Presentation.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Presentation.Infrastructure.Persistence;

public sealed class PresentationDeckConfiguration : IEntityTypeConfiguration<PresentationDeck>
{
    public void Configure(EntityTypeBuilder<PresentationDeck> builder)
    {
        builder.ToTable("Presentaciones");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Category).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ThumbnailUrl).HasMaxLength(500);
        builder.Ignore(x => x.Slides);
        builder.HasIndex(x => x.OwnerId);
        builder.HasIndex(x => x.UpdatedAt);
    }
}

public sealed class PresentationSlideConfiguration : IEntityTypeConfiguration<PresentationSlide>
{
    public void Configure(EntityTypeBuilder<PresentationSlide> builder)
    {
        builder.ToTable("PresentacionDiapositivas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(4000);
        builder.Property(x => x.BackgroundJson).IsRequired();
        builder.Property(x => x.ElementsJson).IsRequired();
        builder.HasIndex(x => new { x.PresentationId, x.Position });
    }
}
