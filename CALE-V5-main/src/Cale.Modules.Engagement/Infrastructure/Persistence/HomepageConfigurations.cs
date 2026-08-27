using Cale.Modules.Engagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Engagement.Infrastructure.Persistence;

public sealed class HomepageSettingsConfiguration : IEntityTypeConfiguration<HomepageSettings>
{
    public void Configure(EntityTypeBuilder<HomepageSettings> builder)
    {
        builder.ToTable("HomepageSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.HeroBadge).HasMaxLength(120).IsRequired();
        builder.Property(x => x.HeroTitle).HasMaxLength(200).IsRequired();
        builder.Property(x => x.HeroTitleHighlight).HasMaxLength(200).IsRequired();
        builder.Property(x => x.HeroDescription).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.HeroCtaPrimaryLabel).HasMaxLength(80).IsRequired();
        builder.Property(x => x.HeroCtaPrimaryPath).HasMaxLength(200).IsRequired();
        builder.Property(x => x.HeroCtaSecondaryLabel).HasMaxLength(80).IsRequired();
        builder.Property(x => x.HeroVideoUrl).HasMaxLength(500);
        builder.Property(x => x.HeroImageUrl).HasMaxLength(500);
        builder.Property(x => x.HeroImageUrlMobile).HasMaxLength(500);
        builder.Property(x => x.HeroImageAlt).HasMaxLength(200).IsRequired();
        builder.Property(x => x.BenefitsJson).IsRequired();
        builder.Property(x => x.StepsJson).IsRequired();
        builder.Property(x => x.StepsSectionTitle).HasMaxLength(200).IsRequired();
        builder.Property(x => x.StepsSectionSubtitle).HasMaxLength(500).IsRequired();
        builder.Property(x => x.SeoTitle).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SeoDescription).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContactEmail).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ContactPhone).HasMaxLength(80).IsRequired();
        builder.Property(x => x.AboutHtml).HasMaxLength(8000).IsRequired();
        builder.Property(x => x.BlogIntro).HasMaxLength(2000).IsRequired();
    }
}

public sealed class HomepageStatSettingConfiguration : IEntityTypeConfiguration<HomepageStatSetting>
{
    public void Configure(EntityTypeBuilder<HomepageStatSetting> builder)
    {
        builder.ToTable("HomepageStatSettings");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Key).IsUnique();
        builder.Property(x => x.Key).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(120).IsRequired();
        builder.Property(x => x.SubLabel).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Icon).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Mode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ManualValue).HasMaxLength(80);
        builder.Property(x => x.LastComputedValue).HasMaxLength(80);
        builder.Property(x => x.LastComputedDisplay).HasMaxLength(80);
    }
}

public sealed class HomepageAuditConfiguration : IEntityTypeConfiguration<HomepageAudit>
{
    public void Configure(EntityTypeBuilder<HomepageAudit> builder)
    {
        builder.ToTable("HomepageAudits");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Area).HasMaxLength(80).IsRequired();
        builder.Property(x => x.StatKey).HasMaxLength(40);
        builder.Property(x => x.PreviousValue).HasMaxLength(200);
        builder.Property(x => x.NewValue).HasMaxLength(200);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasIndex(x => x.CreatedAt);
    }
}
