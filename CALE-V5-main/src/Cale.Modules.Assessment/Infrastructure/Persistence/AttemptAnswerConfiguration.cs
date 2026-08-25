using Cale.Modules.Assessment.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Assessment.Infrastructure.Persistence;

public sealed class AttemptAnswerConfiguration
    : IEntityTypeConfiguration<AttemptAnswer>
{
    public void Configure(EntityTypeBuilder<AttemptAnswer> builder)
    {
        builder.ToTable("RespuestasIntento");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AttemptId).HasColumnName("IntentoId");
        builder.Property(x => x.QuestionId).HasColumnName("PreguntaId");
        builder.Property(x => x.OptionId).HasColumnName("OpcionId");
        builder.Property(x => x.IsCorrect).HasColumnName("EsCorrecta");
        builder.Property(x => x.QuestionTextSnapshot)
            .HasColumnName("PreguntaTextoSnapshot");
        builder.Property(x => x.SelectedOptionSnapshot)
            .HasColumnName("OpcionSeleccionadaSnapshot");
        builder.Property(x => x.CorrectOptionSnapshot)
            .HasColumnName("OpcionCorrectaSnapshot");
        builder.Property(x => x.QuestionTypeSnapshot)
            .HasColumnName("TipoPreguntaSnapshot");
    }
}
