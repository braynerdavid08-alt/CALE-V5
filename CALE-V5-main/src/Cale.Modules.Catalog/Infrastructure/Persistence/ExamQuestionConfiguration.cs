using Cale.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Catalog.Infrastructure.Persistence;

public sealed class ExamQuestionConfiguration
    : IEntityTypeConfiguration<ExamQuestion>
{
    public void Configure(EntityTypeBuilder<ExamQuestion> builder)
    {
        builder.ToTable("ExamenesPreguntas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ExamId).HasColumnName("ExamenId");
        builder.Property(x => x.QuestionId).HasColumnName("PreguntaId");
        builder.Property(x => x.Order).HasColumnName("Orden");
    }
}
