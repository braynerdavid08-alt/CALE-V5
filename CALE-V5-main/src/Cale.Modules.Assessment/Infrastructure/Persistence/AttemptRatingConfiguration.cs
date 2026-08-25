using Cale.Modules.Assessment.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Assessment.Infrastructure.Persistence;

public sealed class AttemptRatingConfiguration
    : IEntityTypeConfiguration<AttemptRating>
{
    public void Configure(EntityTypeBuilder<AttemptRating> builder)
    {
        builder.ToTable("Valoraciones");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).HasColumnName("UsuarioId");
        builder.Property(x => x.BankId).HasColumnName("BancoId");
        builder.Property(x => x.AttemptId).HasColumnName("IntentoId");
        builder.Property(x => x.Stars).HasColumnName("Estrellas");
        builder.Property(x => x.Comment).HasColumnName("Comentario");
        builder.Property(x => x.Critique).HasColumnName("Critica");
        builder.Property(x => x.Reviewed).HasColumnName("Revisada");
        builder.Property(x => x.Hidden).HasColumnName("Oculta");
        builder.Property(x => x.CreatedAt).HasColumnName("CreadoEn");
        builder.Property(x => x.UpdatedAt).HasColumnName("ActualizadoEn");
        builder.HasIndex(x => x.AttemptId).IsUnique();
    }
}
