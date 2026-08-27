using Cale.Modules.Assessment.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Assessment.Infrastructure.Persistence;

public sealed class AttemptConfiguration : IEntityTypeConfiguration<Attempt>
{
    public void Configure(EntityTypeBuilder<Attempt> builder)
    {
        builder.ToTable("Intentos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).HasColumnName("UsuarioId");
        builder.Property(x => x.BankId).HasColumnName("BancoId");
        builder.Property(x => x.ExamId).HasColumnName("ExamenId");
        builder.Property(x => x.Mode).HasColumnName("Modo");
        builder.Property(x => x.TotalQuestions).HasColumnName("TotalPreguntas");
        builder.Property(x => x.CorrectCount).HasColumnName("Aciertos");
        builder.Property(x => x.Percent).HasColumnName("Porcentaje");
        builder.Property(x => x.Passed).HasColumnName("Aprobado");
        builder.Property(x => x.TimeSeconds).HasColumnName("TiempoSegundos");
        builder.Property(x => x.StartedAt).HasColumnName("InicioEn");
        builder.Property(x => x.FinishedAt).HasColumnName("FinEn");
        builder.Property(x => x.ExpiresAt).HasColumnName("ExpiresAt");

        // At most one open attempt per user+exam (practice ExamId null excluded).
        builder.HasIndex(x => new { x.UserId, x.ExamId })
            .IsUnique()
            .HasFilter("\"FinEn\" IS NULL AND \"ExamenId\" IS NOT NULL")
            .HasDatabaseName("IX_Intentos_OpenExam");
    }
}
