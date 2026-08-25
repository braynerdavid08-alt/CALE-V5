using Cale.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Catalog.Infrastructure.Persistence;

public sealed class QuestionOptionConfiguration
    : IEntityTypeConfiguration<QuestionOption>
{
    public void Configure(EntityTypeBuilder<QuestionOption> builder)
    {
        builder.ToTable("OpcionesPregunta");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.QuestionId).HasColumnName("PreguntaId");
        builder.Property(x => x.Text).HasColumnName("Texto").IsRequired();
        builder.Property(x => x.IsCorrect).HasColumnName("EsCorrecta");
        builder.Property(x => x.ImageUrl).HasColumnName("ImagenUrl");
    }
}
