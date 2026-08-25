using Cale.Modules.Assessment.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Assessment.Infrastructure.Persistence;

public sealed class AttemptQuestionConfiguration
    : IEntityTypeConfiguration<AttemptQuestion>
{
    public void Configure(EntityTypeBuilder<AttemptQuestion> builder)
    {
        builder.ToTable("IntentosPreguntas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AttemptId).HasColumnName("IntentoId");
        builder.Property(x => x.QuestionId).HasColumnName("PreguntaId");
        builder.Property(x => x.Order).HasColumnName("Orden");
    }
}
