using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Identity.Infrastructure.Persistence;

public sealed class LegalDocumentVersionConfiguration : IEntityTypeConfiguration<LegalDocumentVersion>
{
    public void Configure(EntityTypeBuilder<LegalDocumentVersion> builder)
    {
        builder.ToTable("LegalVersionesDocumento");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DocumentKey).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Version).HasMaxLength(LegalDocumentVersion.VersionMax).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(LegalDocumentVersion.TitleMax).IsRequired();
        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ChangeSummary).HasMaxLength(LegalDocumentVersion.SummaryMax);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Ignore(x => x.HasPlaceholders);
        builder.HasIndex(x => new { x.DocumentKey, x.Version }).IsUnique();
    }
}

public sealed class UserLegalAcceptanceConfiguration : IEntityTypeConfiguration<UserLegalAcceptance>
{
    public void Configure(EntityTypeBuilder<UserLegalAcceptance> builder)
    {
        builder.ToTable("LegalAceptaciones");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DocumentKey).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Version).HasMaxLength(LegalDocumentVersion.VersionMax).IsRequired();
        builder.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Method).HasMaxLength(20).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(UserLegalAcceptance.IpMax);
        builder.Property(x => x.UserAgent).HasMaxLength(UserLegalAcceptance.UserAgentMax);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.DocumentKey });
        builder.HasIndex(x => x.DocumentVersionId);
    }
}
