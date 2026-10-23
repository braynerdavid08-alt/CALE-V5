using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Identity.Infrastructure.Persistence;

public sealed class PrivateClassRequestConfiguration : IEntityTypeConfiguration<PrivateClassRequest>
{
    public void Configure(EntityTypeBuilder<PrivateClassRequest> builder)
    {
        builder.ToTable("PrivateClassRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Category).HasMaxLength(16).IsRequired();
        builder.Property(x => x.PreferredSchedule).HasMaxLength(PrivateClassRequest.ScheduleMax).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(PrivateClassRequest.MessageMax).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Reply).HasMaxLength(PrivateClassRequest.ReplyMax);
        builder.HasIndex(x => new { x.StudentUserId, x.Status });
        builder.HasIndex(x => new { x.InstructorUserId, x.Status });
    }
}

public sealed class InstructorRatingConfiguration : IEntityTypeConfiguration<InstructorRating>
{
    public void Configure(EntityTypeBuilder<InstructorRating> builder)
    {
        builder.ToTable("InstructorRatings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Comment).HasMaxLength(InstructorRating.CommentMax);
        builder.HasIndex(x => new { x.InstructorUserId, x.RaterUserId }).IsUnique();
        builder.HasIndex(x => x.InstructorUserId);
    }
}
