using Cale.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Catalog.Infrastructure.Persistence;

public sealed class ExamGroupLinkConfiguration
    : IEntityTypeConfiguration<ExamGroupLink>
{
    public void Configure(EntityTypeBuilder<ExamGroupLink> builder)
    {
        builder.ToTable("ExamenesGrupos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExamId).HasColumnName("ExamenId");
        builder.Property(x => x.GroupId).HasColumnName("GrupoId");
        builder.Property(x => x.StartsAt).HasColumnName("FechaInicio");
        builder.Property(x => x.EndsAt).HasColumnName("FechaCierre");
        builder.Property(x => x.CreatedAt).HasColumnName("CreadoEn");
        builder.HasIndex(x => new { x.ExamId, x.GroupId }).IsUnique();
    }
}
