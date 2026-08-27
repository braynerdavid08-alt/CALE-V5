using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Identity.Infrastructure.Persistence;

public sealed class SchoolRegistrationRegistryConfiguration
    : IEntityTypeConfiguration<SchoolRegistrationRegistry>
{
    public void Configure(EntityTypeBuilder<SchoolRegistrationRegistry> builder)
    {
        builder.ToTable("SchoolRegistrationRegistry");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TaxIdKey).HasMaxLength(64).IsRequired();
        builder.Property(x => x.BillingEmailKey).HasMaxLength(320).IsRequired();
        builder.Property(x => x.AccessEmailKey).HasMaxLength(320).IsRequired();
        builder.Property(x => x.PhoneKey).HasMaxLength(32).IsRequired();
        builder.Property(x => x.LegalNameKey).HasMaxLength(250).IsRequired();
        builder.Property(x => x.CityKey).HasMaxLength(120).IsRequired();
        builder.HasIndex(x => x.TaxIdKey);
        builder.HasIndex(x => x.BillingEmailKey);
        builder.HasIndex(x => x.AccessEmailKey);
        builder.HasIndex(x => x.PhoneKey);
    }
}
