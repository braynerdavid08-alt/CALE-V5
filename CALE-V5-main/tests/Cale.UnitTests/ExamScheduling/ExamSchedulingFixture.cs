using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Cale.Modules.Identity.Infrastructure.Persistence;
using Cale.Modules.TheoreticalTraining.Application;
using Cale.Modules.TheoreticalTraining.Domain;
using Cale.Modules.TheoreticalTraining.Infrastructure.Persistence;
using Cale.UnitTests.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cale.UnitTests.ExamScheduling;

/// <summary>
/// Real SQLite file database (unique indexes, filtered indexes and transactions behave like production),
/// two schools and three students. Monday 5 Oct 2026, 08:00 Colombia time.
/// </summary>
public sealed class ExamSchedulingFixture : IDisposable
{
    private readonly string _path;

    public ExamSchedulingFixture()
    {
        _path = Path.Combine(Path.GetTempPath(), $"cale-exams-{Guid.NewGuid():N}.db");
        Clock = new FakeClock(new DateTime(2026, 10, 5, 13, 0, 0, DateTimeKind.Utc));
        using var db = NewDb();
        db.Database.EnsureCreated();

        var now = Clock.UtcNow;
        var schoolA = User.RegisterSchool("Escuela A", "a@school.test", "x", now);
        var schoolB = User.RegisterSchool("Escuela B", "b@school.test", "x", now);
        db.Set<User>().AddRange(schoolA, schoolB);
        db.SaveChanges();
        SchoolA = schoolA.Id;
        SchoolB = schoolB.Id;

        var juan = User.RegisterStudent("Juan Pérez", "juan@test", "x", now, SchoolA);
        var maria = User.RegisterStudent("María López", "maria@test", "x", now, SchoolA);
        var carlos = User.RegisterStudent("Carlos Díaz", "carlos@test", "x", now, SchoolA);
        var ana = User.RegisterStudent("Ana Gómez", "ana@test", "x", now, SchoolA);
        var pedro = User.RegisterStudent("Pedro Ruiz", "pedro@test", "x", now, SchoolB);
        db.Set<User>().AddRange(juan, maria, carlos, ana, pedro);
        db.SaveChanges();
        Juan = juan.Id;
        Maria = maria.Id;
        Carlos = carlos.Id;
        Ana = ana.Id;
        Pedro = pedro.Id;

        db.Set<SchoolStudentEnrollment>().AddRange(
            Enrollment(SchoolA, Juan, now),
            Enrollment(SchoolA, Maria, now),
            Enrollment(SchoolA, Carlos, now),
            Enrollment(SchoolA, Ana, now),
            Enrollment(SchoolB, Pedro, now));
        db.SaveChanges();
    }

    public FakeClock Clock { get; }
    public FakeEligibility Eligibility { get; } = new();
    public FakeNotifications Notifications { get; } = new();
    public int SchoolA { get; }
    public int SchoolB { get; }
    public int Juan { get; }
    public int Maria { get; }
    public int Carlos { get; }
    public int Ana { get; }
    public int Pedro { get; }

    /// <summary>Monday of the test week.</summary>
    public static DateOnly Monday => new(2026, 10, 5);

    public CaleDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<CaleDbContext>()
            .UseSqlite($"Data Source={_path};Pooling=False;Default Timeout=30")
            .Options;
        return new CaleDbContext(
            options,
            new MappingAssemblies(
                typeof(UserConfiguration).Assembly,
                typeof(TheoryExamAppointmentConfiguration).Assembly,
                typeof(Cale.Modules.Assessment.Infrastructure.Persistence.AttemptConfiguration).Assembly));
    }

    public TheoryExamScheduleService Exams(CaleDbContext db) =>
        new(db, Clock, new AlwaysActiveMembership(), Notifications, Eligibility, AutoAuthorizer(db));

    public StudentHoursService Hours(CaleDbContext db) =>
        new(db, Clock, new AlwaysActiveMembership(), Notifications, AutoAuthorizer(db));

    public TheoryExamAutoAuthorizer AutoAuthorizer(CaleDbContext db) =>
        new(db, Clock, new AlwaysActiveMembership(), Notifications, NullLogger<TheoryExamAutoAuthorizer>.Instance);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            File.Delete(_path);
        }
        catch (IOException)
        {
        }
    }

    private static SchoolStudentEnrollment Enrollment(int school, int student, DateTime now) => new()
    {
        SchoolUserId = school,
        StudentUserId = student,
        Status = StudentEnrollmentStatuses.Active,
        LicenseCategories = "B1",
        TheoryExamAuthorized = true,
        CreatedAt = now,
        UpdatedAt = now
    };
}

public sealed class AlwaysActiveMembership : ISchoolMembershipGuard
{
    public Task EnsureActiveAsync(int schoolUserId, CancellationToken ct = default) => Task.CompletedTask;
}

public sealed class FakeEligibility : IExamBookingEligibility
{
    public HashSet<int> Blocked { get; } = [];

    public Task EnsureEligibleAsync(int schoolUserId, int studentUserId, CancellationToken ct) =>
        Blocked.Contains(studentUserId)
            ? throw new DomainException("No autorizado.", 400, "theory_exam_not_authorized")
            : Task.CompletedTask;
}

public sealed class FakeNotifications : INotificationPublisher
{
    private readonly object _gate = new();

    public List<(int UserId, string Title)> Sent { get; } = [];

    public Task NotifyUserAsync(int userId, string title, string message, string type, int? groupId,
        string? relatedEntity, int? relatedId, CancellationToken ct)
    {
        lock (_gate)
        {
            Sent.Add((userId, title));
        }

        return Task.CompletedTask;
    }

    public Task NotifyUsersAsync(IReadOnlyList<int> userIds, string title, string message, string type,
        int? groupId, string? relatedEntity, int? relatedId, CancellationToken ct)
    {
        lock (_gate)
        {
            Sent.AddRange(userIds.Select(id => (id, title)));
        }

        return Task.CompletedTask;
    }

    public Task NotifyUsersAsync(IReadOnlyList<int> userIds, NotificationDraft draft, CancellationToken ct) =>
        NotifyUsersAsync(userIds, draft.Title, draft.Message, draft.Type, null, null, null, ct);
}
