using Cale.Modules.Assessment.Domain.Gamification;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.Assessment.Infrastructure.Persistence;

public sealed class TournamentConfiguration : IEntityTypeConfiguration<Tournament>
{
    public void Configure(EntityTypeBuilder<Tournament> builder)
    {
        builder.ToTable("Torneos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.QuestionMode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.CategoryFilterJson).HasMaxLength(2000);
        builder.Property(x => x.ManualQuestionIdsJson).HasMaxLength(4000);
        builder.Property(x => x.BankIdsJson).HasMaxLength(2000);
        builder.Property(x => x.RowVersion).IsConcurrencyToken();
        builder.HasIndex(x => new { x.SchoolUserId, x.Status });
        builder.HasIndex(x => x.ScheduledAt);
        builder.HasMany(x => x.Participants).WithOne(x => x.Tournament).HasForeignKey(x => x.TournamentId);
        builder.HasMany(x => x.Rounds).WithOne(x => x.Tournament).HasForeignKey(x => x.TournamentId);
    }
}

public sealed class TournamentParticipantConfiguration : IEntityTypeConfiguration<TournamentParticipant>
{
    public void Configure(EntityTypeBuilder<TournamentParticipant> builder)
    {
        builder.ToTable("TorneoParticipantes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DisplayNameSnapshot).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ConnectionId).HasMaxLength(80);
        builder.HasIndex(x => new { x.TournamentId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}

public sealed class TournamentRoundConfiguration : IEntityTypeConfiguration<TournamentRound>
{
    public void Configure(EntityTypeBuilder<TournamentRound> builder)
    {
        builder.ToTable("TorneoRondas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.TournamentId, x.RoundNumber }).IsUnique();
        builder.HasMany(x => x.Matches).WithOne(x => x.Round).HasForeignKey(x => x.RoundId);
    }
}

public sealed class TournamentMatchConfiguration : IEntityTypeConfiguration<TournamentMatch>
{
    public void Configure(EntityTypeBuilder<TournamentMatch> builder)
    {
        builder.ToTable("TorneoEnfrentamientos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.FinishReason).HasMaxLength(30);
        builder.Property(x => x.RowVersion).IsConcurrencyToken();
        builder.HasIndex(x => new { x.TournamentId, x.RoundId, x.SlotIndex }).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasMany(x => x.Questions).WithOne(x => x.Match).HasForeignKey(x => x.MatchId);
        builder.HasMany(x => x.Answers).WithOne().HasForeignKey(x => x.MatchId);
    }
}

public sealed class TournamentMatchQuestionConfiguration : IEntityTypeConfiguration<TournamentMatchQuestion>
{
    public void Configure(EntityTypeBuilder<TournamentMatchQuestion> builder)
    {
        builder.ToTable("TorneoEnfrentamientoPreguntas");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.MatchId, x.Ordinal }).IsUnique();
    }
}

public sealed class TournamentAnswerConfiguration : IEntityTypeConfiguration<TournamentAnswer>
{
    public void Configure(EntityTypeBuilder<TournamentAnswer> builder)
    {
        builder.ToTable("TorneoRespuestas");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.MatchQuestionId, x.UserId }).IsUnique();
        builder.HasIndex(x => x.MatchId);
    }
}

public sealed class TournamentTrophyConfiguration : IEntityTypeConfiguration<TournamentTrophy>
{
    public void Configure(EntityTypeBuilder<TournamentTrophy> builder)
    {
        builder.ToTable("TrofeosTorneo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TournamentNameSnapshot).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ChampionResult).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.TournamentId, x.UserId }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.ObtainedAt });
    }
}
