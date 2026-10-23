using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Api.Services.Assistant;

public sealed class AssistantUsageDailyConfiguration : IEntityTypeConfiguration<AssistantUsageDaily>
{
    public void Configure(EntityTypeBuilder<AssistantUsageDaily> builder)
    {
        builder.ToTable("AssistantUsageDaily");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.Day }).IsUnique();
        builder.HasIndex(x => new { x.SchoolUserId, x.Day });
    }
}

public sealed class AssistantUsageEventConfiguration : IEntityTypeConfiguration<AssistantUsageEvent>
{
    public void Configure(EntityTypeBuilder<AssistantUsageEvent> builder)
    {
        builder.ToTable("AssistantUsageEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Kind).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasIndex(x => new { x.SchoolUserId, x.Day });
        builder.HasIndex(x => new { x.UserId, x.Day });
    }
}
