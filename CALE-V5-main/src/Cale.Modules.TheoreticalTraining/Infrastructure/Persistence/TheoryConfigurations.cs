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
        builder.Property(x => x.Category).HasMaxLength(16).IsRequired();
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
        builder.Property(x => x.LicenseCategories).HasMaxLength(32);
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

public sealed class PracticalVehicleConfiguration : IEntityTypeConfiguration<PracticalVehicle>
{
    public void Configure(EntityTypeBuilder<PracticalVehicle> builder)
    {
        builder.ToTable("PracticalVehicles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Label).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Plate).HasMaxLength(16);
        builder.HasIndex(x => x.SchoolUserId);
    }
}

public sealed class PracticalLessonSessionConfiguration : IEntityTypeConfiguration<PracticalLessonSession>
{
    public void Configure(EntityTypeBuilder<PracticalLessonSession> builder)
    {
        builder.ToTable("PracticalLessonSessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.SchoolUserId, x.SessionDate });
        builder.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId);
    }
}

public sealed class PracticalLessonReservationConfiguration : IEntityTypeConfiguration<PracticalLessonReservation>
{
    public void Configure(EntityTypeBuilder<PracticalLessonReservation> builder)
    {
        builder.ToTable("PracticalLessonReservations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.LessonSessionId, x.StudentUserId });
        builder.HasOne(x => x.LessonSession).WithMany().HasForeignKey(x => x.LessonSessionId);
    }
}

public sealed class SchoolApprenticeProfileConfiguration : IEntityTypeConfiguration<SchoolApprenticeProfile>
{
    public void Configure(EntityTypeBuilder<SchoolApprenticeProfile> builder)
    {
        builder.ToTable("SchoolApprenticeProfiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DocumentType).HasMaxLength(8);
        builder.Property(x => x.DocumentNumber).HasMaxLength(32);
        builder.Property(x => x.Phone).HasMaxLength(32);
        builder.Property(x => x.Address).HasMaxLength(256);
        builder.Property(x => x.ContactEmail).HasMaxLength(256);
        builder.Property(x => x.EnrollmentMonth).HasMaxLength(32);
        builder.Property(x => x.ScheduleSlot).HasMaxLength(32);
        builder.Property(x => x.ReceiptNumber).HasMaxLength(32);
        builder.Property(x => x.PaymentMethod).HasMaxLength(32);
        builder.Property(x => x.BalancePaymentMethod).HasMaxLength(32);
        builder.Property(x => x.BalanceReceiptNumber).HasMaxLength(32);
        builder.Property(x => x.EnrollmentPin).HasMaxLength(32);
        builder.Property(x => x.Notes).HasMaxLength(512);
        builder.HasIndex(x => new { x.SchoolUserId, x.StudentUserId }).IsUnique();
        builder.HasIndex(x => new { x.SchoolUserId, x.DocumentNumber });
    }
}

public sealed class TheoryExamAppointmentConfiguration : IEntityTypeConfiguration<TheoryExamAppointment>
{
    public void Configure(EntityTypeBuilder<TheoryExamAppointment> builder)
    {
        builder.ToTable("TheoryExamAppointments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StudentLabel).HasMaxLength(160);
        builder.Property(x => x.Notes).HasMaxLength(256);
        builder.HasIndex(x => new { x.SchoolUserId, x.ExamDate, x.SlotTime });
    }
}

public sealed class EnrollmentAuthorizationEventConfiguration
    : IEntityTypeConfiguration<EnrollmentAuthorizationEvent>
{
    public void Configure(EntityTypeBuilder<EnrollmentAuthorizationEvent> builder)
    {
        builder.ToTable("EnrollmentAuthorizationEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AuthorizationType).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(16).IsRequired();
        builder.HasIndex(x => new { x.SchoolUserId, x.StudentUserId, x.CreatedAt });
    }
}
