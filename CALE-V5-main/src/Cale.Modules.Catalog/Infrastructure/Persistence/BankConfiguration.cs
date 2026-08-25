using Cale.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Catalog.Infrastructure.Persistence;

public sealed class BankConfiguration : IEntityTypeConfiguration<Bank>
{
    public void Configure(EntityTypeBuilder<Bank> builder)
    {
        builder.ToTable("Bancos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasColumnName("Nombre").IsRequired();
        builder.Property(x => x.Description).HasColumnName("Descripcion");
        builder.Property(x => x.IsActive).HasColumnName("Activo");
        builder.Property(x => x.SeedCompleted).HasColumnName("SeedInicialCompletado");
        builder.Property(x => x.DistributionApplied)
            .HasColumnName("DistribucionRespuestasInicialAplicada");
        builder.Property(x => x.CreatedAt).HasColumnName("CreadoEn");
    }
}
