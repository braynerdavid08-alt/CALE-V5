using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Identity.Infrastructure.Persistence;

public sealed class CommercialPlanConfiguration : IEntityTypeConfiguration<CommercialPlan>
{
    public void Configure(EntityTypeBuilder<CommercialPlan> builder)
    {
        builder.ToTable("CommercialPlans");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.LabelEs).HasMaxLength(80).IsRequired();
        builder.Property(x => x.PriceCop).HasPrecision(18, 2);
        builder.HasIndex(x => x.Code).IsUnique();
    }
}
