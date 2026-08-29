using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cale.Modules.TheoreticalTraining.Infrastructure.Persistence;

public sealed class TheoryTopicConfiguration : IEntityTypeConfiguration<TheoryTopic>
{
    public void Configure(EntityTypeBuilder<TheoryTopic> builder)
    {
        builder.ToTable("TheoryTopics");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Color).HasMaxLength(16).IsRequired();
        builder.HasIndex(x => new { x.SchoolUserId, x.Name });
    }
}

public sealed class TheoryClassroomConfiguration : IEntityTypeConfiguration<TheoryClassroom>
{
    public void Configure(EntityTypeBuilder<TheoryClassroom> builder)
    {
        builder.ToTable("TheoryClassrooms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => x.SchoolUserId);
    }
}

public sealed class TheoryTrainingSettingsConfiguration : IEntityTypeConfiguration<TheoryTrainingSettings>
{
    public void Configure(EntityTypeBuilder<TheoryTrainingSettings> builder)
    {
        builder.ToTable("TheoryTrainingSettings");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.SchoolUserId).IsUnique();
    }
}

public sealed class TheoryClassSessionConfiguration : IEntityTypeConfiguration<TheoryClassSession>
{
    public void Configure(EntityTypeBuilder<TheoryClassSession> builder)
    {
        builder.ToTable("TheoryClassSessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.SchoolUserId, x.SessionDate });
        builder.HasIndex(x => x.ReservationOpenAt);
        builder.HasIndex(x => x.ReservationCloseAt);
        builder.HasOne(x => x.Topic).WithMany().HasForeignKey(x => x.TopicId);
        builder.HasOne(x => x.Classroom).WithMany().HasForeignKey(x => x.ClassroomId);
    }
}

public sealed class TheoryClassReservationConfiguration : IEntityTypeConfiguration<TheoryClassReservation>
{
    public void Configure(EntityTypeBuilder<TheoryClassReservation> builder)
    {
        builder.ToTable("TheoryClassReservations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.ClassSessionId, x.StudentUserId });
        builder.HasIndex(x => x.StudentUserId);
        builder.HasOne(x => x.ClassSession).WithMany().HasForeignKey(x => x.ClassSessionId);
    }
}

public sealed class TheoryAttendanceRecordConfiguration : IEntityTypeConfiguration<TheoryAttendanceRecord>
{
    public void Configure(EntityTypeBuilder<TheoryAttendanceRecord> builder)
    {
        builder.ToTable("TheoryAttendanceRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.ClassSessionId, x.StudentUserId }).IsUnique();
        builder.HasOne(x => x.ClassSession).WithMany().HasForeignKey(x => x.ClassSessionId);
    }
}

public sealed class SchoolStudentEnrollmentConfiguration : IEntityTypeConfiguration<SchoolStudentEnrollment>
{
    public void Configure(EntityTypeBuilder<SchoolStudentEnrollment> builder)
    {
        builder.ToTable("SchoolStudentEnrollments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.AttendanceDayType).HasMaxLength(16);
        builder.HasIndex(x => new { x.SchoolUserId, x.StudentUserId }).IsUnique();
        builder.HasIndex(x => x.StudentUserId);
    }
}

public sealed class StudentDailyCheckInConfiguration : IEntityTypeConfiguration<StudentDailyCheckIn>
{
    public void Configure(EntityTypeBuilder<StudentDailyCheckIn> builder)
    {
        builder.ToTable("StudentDailyCheckIns");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.StudentUserId, x.CheckInDate }).IsUnique();
    }
}
