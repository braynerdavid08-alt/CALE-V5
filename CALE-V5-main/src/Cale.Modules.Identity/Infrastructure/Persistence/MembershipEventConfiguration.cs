using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Identity.Infrastructure.Persistence;

public sealed class MembershipEventConfiguration : IEntityTypeConfiguration<MembershipEvent>
{
    public void Configure(EntityTypeBuilder<MembershipEvent> builder)
    {
        builder.ToTable("MembershipEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SchoolUserId).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.PlanCode).HasMaxLength(32);
        builder.Property(x => x.PlanPriceCop).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => x.SchoolUserId);
        builder.HasIndex(x => x.EventType);
        builder.HasIndex(x => x.CreatedAt);
    }
}
