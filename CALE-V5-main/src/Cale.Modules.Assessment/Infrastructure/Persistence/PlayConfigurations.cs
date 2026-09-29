using Cale.Modules.Assessment.Domain.Gamification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Assessment.Infrastructure.Persistence;

public sealed class DailyChallengeConfiguration : IEntityTypeConfiguration<DailyChallenge>
{
    public void Configure(EntityTypeBuilder<DailyChallenge> builder)
    {
        builder.ToTable("RetosDiarios");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.QuestionIdsJson).IsRequired();
        builder.Property(x => x.AnswersJson).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.ChallengeDate }).IsUnique();
    }
}

public sealed class MistakeReviewConfiguration : IEntityTypeConfiguration<MistakeReview>
{
    public void Configure(EntityTypeBuilder<MistakeReview> builder)
    {
        builder.ToTable("RepasoErrores");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.QuestionId }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.NextDueAt });
    }
}

public sealed class UserAchievementConfiguration : IEntityTypeConfiguration<UserAchievement>
{
    public void Configure(EntityTypeBuilder<UserAchievement> builder)
    {
        builder.ToTable("LogrosUsuario");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(60).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.Code }).IsUnique();
    }
}

public sealed class GameResultConfiguration : IEntityTypeConfiguration<GameResult>
{
    public void Configure(EntityTypeBuilder<GameResult> builder)
    {
        builder.ToTable("PartidasJuego");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Game).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.Game });
        builder.HasIndex(x => x.PlayedAt);
    }
}

public sealed class PlayerProfileConfiguration : IEntityTypeConfiguration<PlayerProfile>
{
    public void Configure(EntityTypeBuilder<PlayerProfile> builder)
    {
        builder.ToTable("PerfilJuego");
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.UserId).ValueGeneratedNever();
    }
}
