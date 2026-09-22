using Cale.Modules.GameShow.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.GameShow.Infrastructure.Persistence;

public sealed class GameShowSessionConfiguration : IEntityTypeConfiguration<GameShowSession>
{
    public void Configure(EntityTypeBuilder<GameShowSession> builder)
    {
        builder.ToTable("GameShowSessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.JoinCode).HasMaxLength(12).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.TeamAName).HasMaxLength(80).IsRequired();
        builder.Property(x => x.TeamBName).HasMaxLength(80).IsRequired();
        builder.Property(x => x.SettingsJson).HasMaxLength(8000);
        builder.HasIndex(x => x.JoinCode).IsUnique();
        builder.HasIndex(x => x.HostUserId);
        builder.HasIndex(x => x.SchoolUserId);
        builder.HasMany(x => x.Rounds)
            .WithOne()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Players)
            .WithOne()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class GameShowRoundConfiguration : IEntityTypeConfiguration<GameShowRound>
{
    public void Configure(EntityTypeBuilder<GameShowRound> builder)
    {
        builder.ToTable("GameShowRounds");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.QuestionText).HasMaxLength(600).IsRequired();
        builder.Property(x => x.Phase).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ControllingTeam).HasMaxLength(8);
        builder.Property(x => x.BuzzWinnerTeam).HasMaxLength(8);
        builder.HasIndex(x => new { x.SessionId, x.SortOrder }).IsUnique();
        builder.HasMany(x => x.Answers)
            .WithOne()
            .HasForeignKey(x => x.RoundId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Attempts)
            .WithOne()
            .HasForeignKey(x => x.RoundId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class GameShowBoardAnswerConfiguration : IEntityTypeConfiguration<GameShowBoardAnswer>
{
    public void Configure(EntityTypeBuilder<GameShowBoardAnswer> builder)
    {
        builder.ToTable("GameShowBoardAnswers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).HasMaxLength(200).IsRequired();
        builder.Property(x => x.AliasesJson).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => new { x.RoundId, x.Rank }).IsUnique();
    }
}

public sealed class GameShowPlayerConfiguration : IEntityTypeConfiguration<GameShowPlayer>
{
    public void Configure(EntityTypeBuilder<GameShowPlayer> builder)
    {
        builder.ToTable("GameShowPlayers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DisplayName).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Team).HasMaxLength(8).IsRequired();
        builder.Property(x => x.ConnectionId).HasMaxLength(128);
        builder.HasIndex(x => x.PlayerToken).IsUnique();
        builder.HasIndex(x => x.SessionId);
    }
}

public sealed class GameShowAttemptConfiguration : IEntityTypeConfiguration<GameShowAttempt>
{
    public void Configure(EntityTypeBuilder<GameShowAttempt> builder)
    {
        builder.ToTable("GameShowAttempts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Team).HasMaxLength(8).IsRequired();
        builder.Property(x => x.RawText).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.RoundId);
    }
}

public sealed class GameShowPackConfiguration : IEntityTypeConfiguration<GameShowPack>
{
    public void Configure(EntityTypeBuilder<GameShowPack> builder)
    {
        builder.ToTable("GameShowPacks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasIndex(x => x.OwnerUserId);
        builder.HasIndex(x => x.SchoolUserId);
        builder.HasIndex(x => x.UpdatedAt);
    }
}

public sealed class GameShowSettingsConfiguration : IEntityTypeConfiguration<GameShowSettings>
{
    public void Configure(EntityTypeBuilder<GameShowSettings> builder)
    {
        builder.ToTable("GameShowSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
    }
}
