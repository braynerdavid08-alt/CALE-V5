using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Identity.Infrastructure.Persistence;

public sealed class SchoolCommercialProfileConfiguration
    : IEntityTypeConfiguration<SchoolCommercialProfile>
{
    public void Configure(EntityTypeBuilder<SchoolCommercialProfile> builder)
    {
        builder.ToTable("SchoolCommercialProfiles");
        builder.HasKey(x => x.SchoolUserId);
        builder.Property(x => x.SchoolUserId).ValueGeneratedNever();
        builder.Property(x => x.PublicationStatus).HasMaxLength(32).IsRequired();
        builder.Property(x => x.About).HasMaxLength(SchoolCommercialProfile.AboutMaxLength).IsRequired();
        builder.Property(x => x.LicenseCategories)
            .HasMaxLength(SchoolCommercialProfile.CategoriesMaxLength)
            .IsRequired();
        builder.Property(x => x.CoverMediaId);
        builder.Property(x => x.LogoMediaId);
        builder.Property(x => x.Slug).HasMaxLength(SchoolCommercialProfile.SlugMaxLength);
        builder.Property(x => x.WhatsApp).HasMaxLength(SchoolCommercialProfile.WhatsAppMaxLength);
        builder.Property(x => x.PublicEmail).HasMaxLength(SchoolCommercialProfile.PublicEmailMaxLength);
        builder.Property(x => x.CommerciallySuspendedAt);
        builder.Property(x => x.CommercialSuspensionReason)
            .HasMaxLength(SchoolCommercialProfile.SuspensionReasonMaxLength);
        builder.Property(x => x.PublishedAt);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Ignore(x => x.IsPublished);
        builder.Ignore(x => x.IsCommerciallySuspended);
        builder.Ignore(x => x.HasMedia);
        builder.Ignore(x => x.IsSheetComplete);
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => x.PublicationStatus);
    }
}
