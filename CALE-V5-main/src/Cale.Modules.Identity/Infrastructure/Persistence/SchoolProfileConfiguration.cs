using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Identity.Infrastructure.Persistence;

public sealed class SchoolProfileConfiguration : IEntityTypeConfiguration<SchoolProfile>
{
    public void Configure(EntityTypeBuilder<SchoolProfile> builder)
    {
        builder.ToTable("SchoolProfiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LegalName).HasMaxLength(250).IsRequired();
        builder.Property(x => x.TaxId).HasMaxLength(32).IsRequired();
        builder.Property(x => x.BillingEmail).HasMaxLength(320).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(300).IsRequired();
        builder.Property(x => x.City).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Department).HasMaxLength(120).IsRequired();
        builder.Property(x => x.PlanCode).HasMaxLength(32).IsRequired();
        builder.Property(x => x.PlanPriceCop).HasPrecision(18, 2);
        builder.Property(x => x.SubscriptionStatus).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.TaxId);
    }
}
