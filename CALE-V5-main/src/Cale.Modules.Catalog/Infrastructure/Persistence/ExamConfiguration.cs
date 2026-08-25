using Cale.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Catalog.Infrastructure.Persistence;

public sealed class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.ToTable("Examenes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasColumnName("Nombre").IsRequired();
        builder.Property(x => x.Description).HasColumnName("Descripcion");
        builder.Property(x => x.BankId).HasColumnName("BancoId");
        builder.Property(x => x.QuestionCount).HasColumnName("NumeroPreguntas");
        builder.Property(x => x.TimeMinutes).HasColumnName("TiempoMinutos");
        builder.Property(x => x.AllowedAttempts).HasColumnName("IntentosPermitidos");
        builder.Property(x => x.Randomize).HasColumnName("Aleatorio");
        builder.Property(x => x.Published).HasColumnName("Publicado");
        builder.Property(x => x.IsActive).HasColumnName("Activo");
        builder.Property(x => x.CreatedById).HasColumnName("CreadoPorId");
        builder.Property(x => x.CreatedAt).HasColumnName("CreadoEn");
        builder.Property(x => x.UpdatedAt).HasColumnName("ActualizadoEn");
        builder.Property(x => x.StartsAt).HasColumnName("FechaInicio");
        builder.Property(x => x.EndsAt).HasColumnName("FechaCierre");
    }
}
