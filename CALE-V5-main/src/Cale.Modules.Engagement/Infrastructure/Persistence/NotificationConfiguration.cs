using Cale.Modules.Engagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Engagement.Infrastructure.Persistence;

public sealed class NotificationConfiguration
    : IEntityTypeConfiguration<AppNotification>
{
    public void Configure(EntityTypeBuilder<AppNotification> builder)
    {
        builder.ToTable("Notificaciones");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).HasColumnName("UsuarioId");
        builder.Property(x => x.Title).HasColumnName("Titulo").HasMaxLength(200);
        builder.Property(x => x.Message).HasColumnName("Mensaje").HasMaxLength(2000);
        builder.Property(x => x.Type).HasColumnName("Tipo").HasMaxLength(40);
        builder.Property(x => x.IsRead).HasColumnName("Leida");
        builder.Property(x => x.CreatedAt).HasColumnName("CreadoEn");
        builder.Property(x => x.ReadAt).HasColumnName("LeidaEn");
        builder.Property(x => x.GroupId).HasColumnName("GrupoId");
        builder.Property(x => x.RelatedEntity).HasColumnName("RelatedEntity").HasMaxLength(80);
        builder.Property(x => x.RelatedId).HasColumnName("RelatedId");
        builder.Property(x => x.Link).HasColumnName("Link").HasMaxLength(300);
        builder.Property(x => x.Priority).HasColumnName("Prioridad").HasMaxLength(20);
        builder.Property(x => x.DedupeKey).HasColumnName("DedupeKey").HasMaxLength(120);
        builder.Property(x => x.IsArchived).HasColumnName("Archivada");

        builder.HasIndex(x => new { x.UserId, x.IsRead, x.IsArchived });
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        builder.HasIndex(x => new { x.UserId, x.DedupeKey });
    }
}

public sealed class NotificationPreferenceConfiguration
    : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences");
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.UserId).ValueGeneratedNever();
        builder.Property(x => x.AcademicEnabled);
        builder.Property(x => x.MembershipEnabled);
        builder.Property(x => x.AdminEnabled);
        builder.Property(x => x.SystemEnabled);
    }
}
