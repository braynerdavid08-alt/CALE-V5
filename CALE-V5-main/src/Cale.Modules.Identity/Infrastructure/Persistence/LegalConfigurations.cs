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

public sealed class AgeDeclarationConfiguration : IEntityTypeConfiguration<AgeDeclaration>
{
    public void Configure(EntityTypeBuilder<AgeDeclaration> builder)
    {
        builder.ToTable("DeclaracionesEdad");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BirthDate).IsRequired();
        builder.Property(x => x.GuardianName).HasMaxLength(AgeDeclaration.GuardianNameMax);
        builder.Property(x => x.GuardianRelationship).HasMaxLength(30);
        builder.Property(x => x.GuardianEmail).HasMaxLength(AgeDeclaration.GuardianEmailMax);
        builder.Property(x => x.GuardianStatus).HasMaxLength(20).IsRequired();
        builder.Property(x => x.GuardianTokenHash).HasMaxLength(64);
        builder.Ignore(x => x.AwaitsGuardian);
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.GuardianTokenHash);
    }
}

public sealed class PqrRequestConfiguration : IEntityTypeConfiguration<PqrRequest>
{
    public void Configure(EntityTypeBuilder<PqrRequest> builder)
    {
        builder.ToTable("SolicitudesPqr");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ContactName).HasMaxLength(PqrRequest.NameMax).IsRequired();
        builder.Property(x => x.ContactEmail).HasMaxLength(PqrRequest.EmailMax).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(PqrRequest.SubjectMax).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(PqrRequest.MessageMax).IsRequired();
        builder.Property(x => x.Response).HasMaxLength(PqrRequest.ResponseMax);
        builder.Property(x => x.TrackingCodeHash).HasMaxLength(64).IsRequired();
        builder.Ignore(x => x.Radicado);
        builder.Ignore(x => x.IsClosed);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.TrackingCodeHash);
        builder.HasIndex(x => x.Status);
    }
}

public sealed class PqrEventConfiguration : IEntityTypeConfiguration<PqrEvent>
{
    public void Configure(EntityTypeBuilder<PqrEvent> builder)
    {
        builder.ToTable("SolicitudesPqrEventos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(30).IsRequired();
        builder.Property(x => x.FromStatus).HasMaxLength(20);
        builder.Property(x => x.ToStatus).HasMaxLength(20);
        builder.Property(x => x.Note).HasMaxLength(PqrEvent.NoteMax);
        builder.HasIndex(x => x.PqrRequestId);
    }
}

public sealed class GuardianConsentEventConfiguration : IEntityTypeConfiguration<GuardianConsentEvent>
{
    public void Configure(EntityTypeBuilder<GuardianConsentEvent> builder)
    {
        builder.ToTable("AutorizacionesMenoresEventos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(30).IsRequired();
        builder.Property(x => x.DocumentVersions).HasMaxLength(300);
        builder.Property(x => x.IpAddress).HasMaxLength(UserLegalAcceptance.IpMax);
        builder.Property(x => x.UserAgent).HasMaxLength(UserLegalAcceptance.UserAgentMax);
        builder.Property(x => x.Note).HasMaxLength(GuardianConsentEvent.NoteMax);
        builder.HasIndex(x => x.UserId);
    }
}
