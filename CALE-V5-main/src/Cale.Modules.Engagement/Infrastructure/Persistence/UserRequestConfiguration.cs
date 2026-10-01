using Cale.Modules.Engagement.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Engagement.Infrastructure.Persistence;

public sealed class UserRequestConfiguration : IEntityTypeConfiguration<UserRequest>
{
    public void Configure(EntityTypeBuilder<UserRequest> builder)
    {
        builder.ToTable("SolicitudesUsuario");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.UserRole).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Kind).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.Message).HasMaxLength(4000);
        builder.Property(x => x.AdminNote).HasMaxLength(1000);
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => x.UserId);
    }
}
