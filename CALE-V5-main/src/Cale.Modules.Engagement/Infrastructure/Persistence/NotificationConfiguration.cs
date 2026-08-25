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
        builder.Property(x => x.Title).HasColumnName("Titulo");
        builder.Property(x => x.Message).HasColumnName("Mensaje");
        builder.Property(x => x.Type).HasColumnName("Tipo");
        builder.Property(x => x.IsRead).HasColumnName("Leida");
        builder.Property(x => x.CreatedAt).HasColumnName("CreadoEn");
        builder.Property(x => x.GroupId).HasColumnName("GrupoId");
        builder.Property(x => x.RelatedEntity).HasColumnName("RelatedEntity");
        builder.Property(x => x.RelatedId).HasColumnName("RelatedId");
    }
}
