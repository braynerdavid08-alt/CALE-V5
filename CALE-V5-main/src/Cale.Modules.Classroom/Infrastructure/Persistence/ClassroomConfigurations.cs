using Cale.Modules.Classroom.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Classroom.Infrastructure.Persistence;

public sealed class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable("Grupos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasColumnName("Nombre").IsRequired();
        builder.Property(x => x.Code).HasColumnName("Codigo").HasMaxLength(450);
        builder.Property(x => x.TeacherId).HasColumnName("ProfesorId");
        builder.Property(x => x.IsActive).HasColumnName("Activo");
        builder.Property(x => x.CreatedAt).HasColumnName("CreadoEn");
        builder.Property(x => x.Description).HasColumnName("Descripcion");
        builder.Property(x => x.StartsOn).HasColumnName("FechaInicio");
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> builder)
    {
        builder.ToTable("GruposUsuarios");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GroupId).HasColumnName("GrupoId");
        builder.Property(x => x.UserId).HasColumnName("UsuarioId");
        builder.Property(x => x.Status).HasColumnName("Estado");
        builder.Property(x => x.CreatedAt).HasColumnName("CreadoEn");
    }
}

public sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("AvisosGrupo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GroupId).HasColumnName("GrupoId");
        builder.Property(x => x.AuthorId).HasColumnName("AutorId");
        builder.Property(x => x.Title).HasColumnName("Titulo");
        builder.Property(x => x.Body).HasColumnName("Contenido");
        builder.Property(x => x.IsActive).HasColumnName("Activo");
        builder.Property(x => x.CreatedAt).HasColumnName("CreadoEn");
        builder.Property(x => x.UpdatedAt).HasColumnName("ActualizadoEn");
    }
}

public sealed class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.ToTable("MaterialesGrupo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GroupId).HasColumnName("GrupoId");
        builder.Property(x => x.AuthorId).HasColumnName("AutorId");
        builder.Property(x => x.Module).HasColumnName("Modulo");
        builder.Property(x => x.Title).HasColumnName("Titulo");
        builder.Property(x => x.Description).HasColumnName("Descripcion");
        builder.Property(x => x.Type).HasColumnName("Tipo");
        builder.Property(x => x.Url).HasColumnName("Url");
        builder.Property(x => x.TextContent).HasColumnName("ContenidoTexto");
        builder.Property(x => x.IsActive).HasColumnName("Activo");
        builder.Property(x => x.CreatedAt).HasColumnName("CreadoEn");
    }
}

public sealed class GroupActivityConfiguration : IEntityTypeConfiguration<GroupActivity>
{
    public void Configure(EntityTypeBuilder<GroupActivity> builder)
    {
        builder.ToTable("ActividadesGrupo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GroupId).HasColumnName("GrupoId");
        builder.Property(x => x.AuthorId).HasColumnName("AutorId");
        builder.Property(x => x.Type).HasColumnName("Tipo");
        builder.Property(x => x.Title).HasColumnName("Titulo");
        builder.Property(x => x.Description).HasColumnName("Descripcion");
        builder.Property(x => x.Instructions).HasColumnName("Instrucciones");
        builder.Property(x => x.PublishedAt).HasColumnName("FechaPublicacion");
        builder.Property(x => x.DueAt).HasColumnName("FechaLimite");
        builder.Property(x => x.MaxScore).HasColumnName("PuntajeMaximo");
        builder.Property(x => x.AttachmentUrl).HasColumnName("AdjuntoUrl");
        builder.Property(x => x.IsActive).HasColumnName("Activo");
        builder.Property(x => x.CreatedAt).HasColumnName("CreadoEn");
    }
}

public sealed class ActivitySubmissionConfiguration
    : IEntityTypeConfiguration<ActivitySubmission>
{
    public void Configure(EntityTypeBuilder<ActivitySubmission> builder)
    {
        builder.ToTable("EntregasActividad");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActivityId).HasColumnName("ActividadId");
        builder.Property(x => x.UserId).HasColumnName("UsuarioId");
        builder.Property(x => x.TextContent).HasColumnName("ContenidoTexto");
        builder.Property(x => x.FileUrl).HasColumnName("ArchivoUrl");
        builder.Property(x => x.SubmittedAt).HasColumnName("EntregadoEn");
        builder.Property(x => x.Score).HasColumnName("Calificacion");
        builder.Property(x => x.TeacherComment).HasColumnName("ComentarioDocente");
        builder.Property(x => x.Status).HasColumnName("Estado");
        builder.HasIndex(x => new { x.ActivityId, x.UserId }).IsUnique();
    }
}
