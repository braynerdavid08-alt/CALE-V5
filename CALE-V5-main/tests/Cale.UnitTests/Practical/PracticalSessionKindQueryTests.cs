using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Infrastructure.Persistence;
using Cale.Modules.Identity.Infrastructure.Persistence;
using Cale.Modules.TheoreticalTraining.Domain;
using Cale.Modules.TheoreticalTraining.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Cale.UnitTests.Practical;

/// <summary>
/// Regression: school practical students endpoint crashed because
/// <c>PracticalSessionKinds.IsExam()</c> is not EF-translatable inside IQueryable.
/// </summary>
public sealed class PracticalSessionKindQueryTests : IDisposable
{
    private readonly string _path;
    private readonly CaleDbContext _db;

    public PracticalSessionKindQueryTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"cale-practical-kind-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<CaleDbContext>()
            .UseSqlite($"Data Source={_path};Pooling=False;Default Timeout=30")
            .Options;
        _db = new CaleDbContext(
            options,
            new MappingAssemblies(
                typeof(UserConfiguration).Assembly,
                typeof(TheoryExamAppointmentConfiguration).Assembly,
                typeof(AttemptConfiguration).Assembly));
        _db.Database.EnsureCreated();

        var vehicle = new PracticalVehicle
        {
            SchoolUserId = 1,
            Label = "Moto 1",
            Plate = "ABC12D",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Set<PracticalVehicle>().Add(vehicle);
        _db.SaveChanges();

        var lesson = new PracticalLessonSession
        {
            SchoolUserId = 1,
            SessionDate = new DateOnly(2026, 11, 4),
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(11, 0),
            InstructorUserId = 2,
            VehicleId = vehicle.Id,
            Capacity = 1,
            Status = PracticalLessonStatuses.Scheduled,
            Kind = PracticalSessionKinds.Lesson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var exam = new PracticalLessonSession
        {
            SchoolUserId = 1,
            SessionDate = new DateOnly(2026, 11, 5),
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(11, 0),
            InstructorUserId = 2,
            VehicleId = vehicle.Id,
            Capacity = 1,
            Status = PracticalLessonStatuses.Scheduled,
            Kind = PracticalSessionKinds.Exam,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Set<PracticalLessonSession>().AddRange(lesson, exam);
        _db.SaveChanges();

        _db.Set<PracticalLessonReservation>().AddRange(
            new PracticalLessonReservation
            {
                LessonSessionId = lesson.Id,
                StudentUserId = 10,
                Status = PracticalReservationStatuses.Attended,
                ReservedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new PracticalLessonReservation
            {
                LessonSessionId = exam.Id,
                StudentUserId = 10,
                Status = PracticalReservationStatuses.Reserved,
                ReservedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Kind_constant_compare_translates_and_filters_practice_vs_exam()
    {
        var practice = await _db.Set<PracticalLessonReservation>()
            .Include(x => x.LessonSession)
            .Where(x => x.StudentUserId == 10
                && x.LessonSession!.SchoolUserId == 1
                && x.LessonSession.Kind != PracticalSessionKinds.Exam
                && PracticalReservationStatuses.OccupiesSeatStatuses.Contains(x.Status))
            .ToListAsync();

        var exams = await _db.Set<PracticalLessonReservation>()
            .Include(x => x.LessonSession)
            .Where(x => x.StudentUserId == 10
                && x.LessonSession!.SchoolUserId == 1
                && x.LessonSession.Kind == PracticalSessionKinds.Exam
                && PracticalReservationStatuses.OccupiesSeatStatuses.Contains(x.Status))
            .ToListAsync();

        Assert.Single(practice);
        Assert.Equal(PracticalSessionKinds.Lesson, practice[0].LessonSession!.Kind);
        Assert.Single(exams);
        Assert.Equal(PracticalSessionKinds.Exam, exams[0].LessonSession!.Kind);
    }

    [Fact]
    public async Task IsExam_method_inside_iqueryable_cannot_translate()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            _ = await _db.Set<PracticalLessonReservation>()
                .Include(x => x.LessonSession)
                .Where(x => x.StudentUserId == 10
                    && PracticalSessionKinds.IsExam(x.LessonSession!.Kind))
                .ToListAsync();
        });
    }

    public void Dispose()
    {
        _db.Dispose();
        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
