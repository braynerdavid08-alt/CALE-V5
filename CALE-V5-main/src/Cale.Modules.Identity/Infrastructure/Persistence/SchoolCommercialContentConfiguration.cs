using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Identity.Infrastructure.Persistence;

public sealed class SchoolCommercialPostConfiguration : IEntityTypeConfiguration<SchoolCommercialPost>
{
    public void Configure(EntityTypeBuilder<SchoolCommercialPost> builder)
    {
        builder.ToTable("SchoolCommercialPosts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Body).HasMaxLength(SchoolCommercialPost.BodyMaxLength).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.SchoolUserId, x.Status });
    }
}

public sealed class SchoolCommercialPromotionConfiguration
    : IEntityTypeConfiguration<SchoolCommercialPromotion>
{
    public void Configure(EntityTypeBuilder<SchoolCommercialPromotion> builder)
    {
        builder.ToTable("SchoolCommercialPromotions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(SchoolCommercialPromotion.TitleMaxLength).IsRequired();
        builder.Property(x => x.Body).HasMaxLength(SchoolCommercialPromotion.BodyMaxLength).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.SchoolUserId, x.Status, x.StartsOn, x.EndsOn });
    }
}

public sealed class SchoolCommercialEventConfiguration : IEntityTypeConfiguration<SchoolCommercialEvent>
{
    public void Configure(EntityTypeBuilder<SchoolCommercialEvent> builder)
    {
        builder.ToTable("SchoolCommercialEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.SchoolUserId, x.EventType, x.CreatedAt });
    }
}
