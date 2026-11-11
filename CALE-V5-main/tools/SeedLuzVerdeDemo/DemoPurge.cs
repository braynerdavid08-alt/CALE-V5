using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Catalog.Domain;
using Cale.Modules.Identity.Domain;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace SeedLuzVerdeDemo;

internal static class DemoPurge
{
    public static async Task<DemoPurgeResult> PurgeAsync(CaleDbContext db, CancellationToken ct)
    {
        // Load candidates then filter in memory — EF cannot translate IsDemoEmail.
        var allUsers = await db.Set<User>().AsNoTracking().ToListAsync(ct);
        var demoUsers = allUsers.Where(u => DemoMarkers.IsDemoEmail(u.Email)).ToList();

        var schoolIds = demoUsers
            .Where(u => u.Role is "School" or "Escuela")
            .Select(u => u.Id)
            .ToHashSet();

        // Also catch schools by legal name if emails were renamed.
        var profilesByName = await db.Set<SchoolProfile>()
            .Where(p => p.LegalName.StartsWith(DemoMarkers.LegalNamePrefix))
            .Select(p => p.UserId)
            .ToListAsync(ct);
        foreach (var id in profilesByName)
        {
            schoolIds.Add(id);
        }

        // Re-fetch tracked users for deletion (only DEMO).
        var demoUserIds = demoUsers.Select(u => u.Id).ToHashSet();
        foreach (var id in schoolIds)
        {
            demoUserIds.Add(id);
        }

        var trackedDemoUsers = await db.Set<User>()
            .Where(u => demoUserIds.Contains(u.Id))
            .ToListAsync(ct);
        // Extra safety: never delete a user that is not DEMO-tagged by email or school DEMO profile.
        trackedDemoUsers = trackedDemoUsers
            .Where(u => DemoMarkers.IsDemoEmail(u.Email) || schoolIds.Contains(u.Id)
                || (u.SchoolId is int sid && schoolIds.Contains(sid) && DemoMarkers.IsDemoEmail(u.Email)))
            .ToList();

        var studentIds = trackedDemoUsers
            .Where(u => schoolIds.Contains(u.SchoolId ?? -1) || DemoMarkers.IsDemoEmail(u.Email))
            .Select(u => u.Id)
            .ToHashSet();

        var userIds = trackedDemoUsers.Select(u => u.Id).ToHashSet();
        foreach (var id in schoolIds)
        {
            userIds.Add(id);
        }

        var removed = new DemoPurgeResult();

        if (schoolIds.Count == 0 && userIds.Count == 0)
        {
            // Still remove orphan DEMO exams/banks.
            removed.Exams = await RemoveDemoCatalogAsync(db, ct);
            await db.SaveChangesAsync(ct);
            return removed;
        }

        async Task<int> RemoveWhereAsync<T>(Func<IQueryable<T>, IQueryable<T>> filter)
            where T : class
        {
            var rows = await filter(db.Set<T>()).ToListAsync(ct);
            if (rows.Count == 0)
            {
                return 0;
            }

            db.Set<T>().RemoveRange(rows);
            return rows.Count;
        }

        removed.Waitlist = await RemoveWhereAsync<ScheduleWaitlistEntry>(q =>
            q.Where(x => schoolIds.Contains(x.SchoolUserId) || studentIds.Contains(x.StudentUserId)));

        removed.ExamAppointments = await RemoveWhereAsync<TheoryExamAppointment>(q =>
            q.Where(x => schoolIds.Contains(x.SchoolUserId)
                || (x.StudentUserId != null && studentIds.Contains(x.StudentUserId.Value))));

        removed.Attendance = await RemoveWhereAsync<TheoryAttendanceRecord>(q =>
            q.Where(x => studentIds.Contains(x.StudentUserId)
                || (x.ClassSession != null && schoolIds.Contains(x.ClassSession.SchoolUserId))));

        // Attendance may not include navigation — delete by session ids.
        var theorySessionIds = await db.Set<TheoryClassSession>()
            .Where(x => schoolIds.Contains(x.SchoolUserId))
            .Select(x => x.Id)
            .ToListAsync(ct);
        if (theorySessionIds.Count > 0)
        {
            var att = await db.Set<TheoryAttendanceRecord>()
                .Where(x => theorySessionIds.Contains(x.ClassSessionId))
                .ToListAsync(ct);
            removed.Attendance += att.Count;
            db.Set<TheoryAttendanceRecord>().RemoveRange(att);

            var res = await db.Set<TheoryClassReservation>()
                .Where(x => theorySessionIds.Contains(x.ClassSessionId))
                .ToListAsync(ct);
            removed.TheoryReservations += res.Count;
            db.Set<TheoryClassReservation>().RemoveRange(res);
        }

        removed.TheoryReservations += await RemoveWhereAsync<TheoryClassReservation>(q =>
            q.Where(x => studentIds.Contains(x.StudentUserId)));

        var theorySessions = await db.Set<TheoryClassSession>()
            .Where(x => schoolIds.Contains(x.SchoolUserId))
            .ToListAsync(ct);
        removed.TheorySessions = theorySessions.Count;
        db.Set<TheoryClassSession>().RemoveRange(theorySessions);

        var practicalSessionIds = await db.Set<PracticalLessonSession>()
            .Where(x => schoolIds.Contains(x.SchoolUserId))
            .Select(x => x.Id)
            .ToListAsync(ct);
        if (practicalSessionIds.Count > 0)
        {
            var pr = await db.Set<PracticalLessonReservation>()
                .Where(x => practicalSessionIds.Contains(x.LessonSessionId)
                    || studentIds.Contains(x.StudentUserId))
                .ToListAsync(ct);
            removed.PracticalReservations = pr.Count;
            db.Set<PracticalLessonReservation>().RemoveRange(pr);
        }
        else
        {
            removed.PracticalReservations = await RemoveWhereAsync<PracticalLessonReservation>(q =>
                q.Where(x => studentIds.Contains(x.StudentUserId)));
        }

        var practicalSessions = await db.Set<PracticalLessonSession>()
            .Where(x => schoolIds.Contains(x.SchoolUserId))
            .ToListAsync(ct);
        removed.PracticalSessions = practicalSessions.Count;
        db.Set<PracticalLessonSession>().RemoveRange(practicalSessions);

        removed.Vehicles = await RemoveWhereAsync<PracticalVehicle>(q =>
            q.Where(x => schoolIds.Contains(x.SchoolUserId)));

        removed.Abonos = await RemoveWhereAsync<ApprenticePaymentAbono>(q =>
            q.Where(x => schoolIds.Contains(x.SchoolUserId) || studentIds.Contains(x.StudentUserId)));

        removed.HourAdjustments = await RemoveWhereAsync<TrainingHoursAdjustment>(q =>
            q.Where(x => schoolIds.Contains(x.SchoolUserId) || studentIds.Contains(x.StudentUserId)));

        removed.AuthEvents = await RemoveWhereAsync<EnrollmentAuthorizationEvent>(q =>
            q.Where(x => schoolIds.Contains(x.SchoolUserId) || studentIds.Contains(x.StudentUserId)));

        removed.Audit = await RemoveWhereAsync<SchoolAuditEntry>(q =>
            q.Where(x => schoolIds.Contains(x.SchoolUserId)));

        removed.Profiles = await RemoveWhereAsync<SchoolApprenticeProfile>(q =>
            q.Where(x => schoolIds.Contains(x.SchoolUserId) || studentIds.Contains(x.StudentUserId)));

        removed.Enrollments = await RemoveWhereAsync<SchoolStudentEnrollment>(q =>
            q.Where(x => schoolIds.Contains(x.SchoolUserId) || studentIds.Contains(x.StudentUserId)));

        removed.ExamOverrides = await RemoveWhereAsync<TheoryExamScheduleOverride>(q =>
            q.Where(x => schoolIds.Contains(x.SchoolUserId)));

        removed.ExamTemplates = await RemoveWhereAsync<TheoryExamScheduleTemplate>(q =>
            q.Where(x => schoolIds.Contains(x.SchoolUserId)));

        removed.Settings = await RemoveWhereAsync<TheoryTrainingSettings>(q =>
            q.Where(x => schoolIds.Contains(x.SchoolUserId)));

        removed.Classrooms = await RemoveWhereAsync<TheoryClassroom>(q =>
            q.Where(x => schoolIds.Contains(x.SchoolUserId)));

        removed.Topics = await RemoveWhereAsync<TheoryTopic>(q =>
            q.Where(x => schoolIds.Contains(x.SchoolUserId)));

        removed.CheckIns = await RemoveWhereAsync<StudentDailyCheckIn>(q =>
            q.Where(x => studentIds.Contains(x.StudentUserId)));

        if (studentIds.Count > 0)
        {
            var attempts = await db.Set<Attempt>()
                .Where(a => studentIds.Contains(a.UserId))
                .ToListAsync(ct);
            removed.Attempts = attempts.Count;
            db.Set<Attempt>().RemoveRange(attempts);
        }

        removed.MembershipEvents = await RemoveWhereAsync<MembershipEvent>(q =>
            q.Where(x => schoolIds.Contains(x.SchoolUserId)
                || (x.ActorUserId != null && userIds.Contains(x.ActorUserId.Value))));

        var schoolProfiles = await db.Set<SchoolProfile>()
            .Where(p => schoolIds.Contains(p.UserId)
                || p.LegalName.StartsWith(DemoMarkers.LegalNamePrefix))
            .ToListAsync(ct);
        removed.SchoolProfiles = schoolProfiles.Count;
        db.Set<SchoolProfile>().RemoveRange(schoolProfiles);

        // Only remove DEMO-marked users (never a real client that somehow shares SchoolId).
        var linked = await db.Set<User>()
            .Where(u => userIds.Contains(u.Id)
                || (u.SchoolId != null && schoolIds.Contains(u.SchoolId.Value)))
            .ToListAsync(ct);
        linked = linked.Where(u =>
            DemoMarkers.IsDemoEmail(u.Email)
            || schoolIds.Contains(u.Id)
            || (u.SchoolId is int sid && schoolIds.Contains(sid) && DemoMarkers.IsDemoEmail(u.Email)))
            .ToList();
        removed.Users = linked.Count;
        db.Set<User>().RemoveRange(linked);

        removed.Exams = await RemoveDemoCatalogAsync(db, ct);

        await db.SaveChangesAsync(ct);
        return removed;
    }

    private static async Task<int> RemoveDemoCatalogAsync(CaleDbContext db, CancellationToken ct)
    {
        var exams = await db.Set<Exam>()
            .Where(e => e.Name == DemoMarkers.ExamName || e.Name.StartsWith("DEMO "))
            .ToListAsync(ct);
        var count = exams.Count;
        db.Set<Exam>().RemoveRange(exams);

        var banks = await db.Set<Bank>()
            .Where(b => b.Name == DemoMarkers.BankName || b.Name.StartsWith("DEMO "))
            .ToListAsync(ct);
        count += banks.Count;
        db.Set<Bank>().RemoveRange(banks);
        return count;
    }
}

internal sealed class DemoPurgeResult
{
    public int Waitlist { get; set; }
    public int ExamAppointments { get; set; }
    public int Attendance { get; set; }
    public int TheoryReservations { get; set; }
    public int TheorySessions { get; set; }
    public int PracticalReservations { get; set; }
    public int PracticalSessions { get; set; }
    public int Vehicles { get; set; }
    public int Abonos { get; set; }
    public int HourAdjustments { get; set; }
    public int AuthEvents { get; set; }
    public int Audit { get; set; }
    public int Profiles { get; set; }
    public int Enrollments { get; set; }
    public int ExamOverrides { get; set; }
    public int ExamTemplates { get; set; }
    public int Settings { get; set; }
    public int Classrooms { get; set; }
    public int Topics { get; set; }
    public int CheckIns { get; set; }
    public int Attempts { get; set; }
    public int MembershipEvents { get; set; }
    public int SchoolProfiles { get; set; }
    public int Users { get; set; }
    public int Exams { get; set; }

    public override string ToString() =>
        $"users={Users}, schools={SchoolProfiles}, enrollments={Enrollments}, " +
        $"theorySessions={TheorySessions}, practicalSessions={PracticalSessions}, " +
        $"examsAppointments={ExamAppointments}, attempts={Attempts}, abonos={Abonos}";
}
