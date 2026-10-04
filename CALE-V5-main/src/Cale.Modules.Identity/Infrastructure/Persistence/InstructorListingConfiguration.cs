using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Identity.Infrastructure.Persistence;

public sealed class InstructorListingConfiguration : IEntityTypeConfiguration<InstructorListing>
{
    public void Configure(EntityTypeBuilder<InstructorListing> builder)
    {
        builder.ToTable("InstructorListings");
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.UserId).ValueGeneratedNever();
        builder.Property(x => x.CreatedAt).IsRequired();
    }
}
