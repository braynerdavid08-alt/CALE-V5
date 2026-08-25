using Cale.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Catalog.Infrastructure.Persistence;

public sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Preguntas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CreatedById).HasColumnName("CreadoPorId");
        builder.Property(x => x.BankId).HasColumnName("BancoId");
        builder.Property(x => x.BlockId).HasColumnName("BloqueId");
        builder.Property(x => x.Text).HasColumnName("Texto").IsRequired();
        builder.Property(x => x.Type).HasColumnName("Tipo").HasMaxLength(40);
        builder.Property(x => x.Subject).HasColumnName("Materia");
        builder.Property(x => x.Topic).HasColumnName("Tema");
        builder.Property(x => x.Subtopic).HasColumnName("Subtema");
        builder.Property(x => x.Difficulty).HasColumnName("Dificultad");
        builder.Property(x => x.ImageUrl).HasColumnName("ImagenUrl");
        builder.Property(x => x.Source).HasColumnName("Fuente");
        builder.Property(x => x.CreatedAt).HasColumnName("CreadoEn");
        builder.Property(x => x.UpdatedAt).HasColumnName("ActualizadoEn");
        builder.Property(x => x.Explanation).HasColumnName("Explicacion");
        builder.Property(x => x.WhyIncorrect).HasColumnName("PorQueIncorrectas");
        builder.Property(x => x.Hint).HasColumnName("Pista");
        builder.Property(x => x.IsActive).HasColumnName("Activa");
        builder.HasMany(x => x.Options)
            .WithOne()
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Options).UsePropertyAccessMode(
            PropertyAccessMode.Field);
    }
}
