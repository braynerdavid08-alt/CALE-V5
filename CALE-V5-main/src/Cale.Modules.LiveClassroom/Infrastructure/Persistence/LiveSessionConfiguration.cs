using Cale.Modules.LiveClassroom.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.LiveClassroom.Infrastructure.Persistence;

public sealed class LiveSessionConfiguration : IEntityTypeConfiguration<LiveSession>
{
    public void Configure(EntityTypeBuilder<LiveSession> builder)
    {
        builder.ToTable("LiveSessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.JoinCode).HasMaxLength(12).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Mode).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ConfigJson).HasMaxLength(4000).IsRequired();
        builder.HasIndex(x => x.JoinCode).IsUnique();
        builder.HasIndex(x => x.HostUserId);
        builder.HasMany(x => x.Participants)
            .WithOne()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Questions)
            .WithOne()
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class LiveParticipantConfiguration : IEntityTypeConfiguration<LiveParticipant>
{
    public void Configure(EntityTypeBuilder<LiveParticipant> builder)
    {
        builder.ToTable("LiveParticipants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DisplayName).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ConnectionId).HasMaxLength(128);
        builder.HasIndex(x => x.ParticipantToken).IsUnique();
        builder.HasIndex(x => x.SessionId);
    }
}

public sealed class LiveSessionQuestionConfiguration : IEntityTypeConfiguration<LiveSessionQuestion>
{
    public void Configure(EntityTypeBuilder<LiveSessionQuestion> builder)
    {
        builder.ToTable("LiveSessionQuestions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SnapshotJson).IsRequired();
        builder.Property(x => x.Topic).HasMaxLength(200);
        builder.Property(x => x.Difficulty).HasMaxLength(64);
        builder.HasIndex(x => new { x.SessionId, x.SortOrder }).IsUnique();
        builder.HasMany(x => x.Answers)
            .WithOne()
            .HasForeignKey(x => x.SessionQuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class LiveAnswerConfiguration : IEntityTypeConfiguration<LiveAnswer>
{
    public void Configure(EntityTypeBuilder<LiveAnswer> builder)
    {
        builder.ToTable("LiveAnswers");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.SessionQuestionId, x.ParticipantId }).IsUnique();
        builder.HasIndex(x => x.ParticipantId);
    }
}
