using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Identity.Infrastructure.Persistence;

public sealed class AccountStatusEventConfiguration : IEntityTypeConfiguration<AccountStatusEvent>
{
    public void Configure(EntityTypeBuilder<AccountStatusEvent> builder)
    {
        builder.ToTable("HistorialEstadoCuenta");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(AccountStatusEvent.ReasonMax).IsRequired();
        builder.Property(x => x.Evidence).HasMaxLength(AccountStatusEvent.EvidenceMax);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Ignore(x => x.IsSuspension);
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
    }
}
