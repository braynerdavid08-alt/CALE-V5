using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Identity.Infrastructure.Persistence;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Usuarios");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name)
            .HasColumnName("Nombre")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(x => x.Email)
            .HasMaxLength(320)
            .IsRequired();
        builder.Property(x => x.PasswordHash).IsRequired();
        builder.Property(x => x.Role)
            .HasColumnName("Rol")
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(x => x.SchoolId);
        builder.Property(x => x.IsActive).HasColumnName("Activo");
        builder.Property(x => x.CreatedAt).HasColumnName("CreadoEn");
        builder.Property(x => x.LastLoginAt).HasColumnName("UltimoAccesoEn");
        builder.Property(x => x.MustChangePassword)
            .HasColumnName("DebeCambiarClave")
            .IsRequired();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.SchoolId);
        builder.HasIndex(x => x.LastLoginAt);
    }
}
