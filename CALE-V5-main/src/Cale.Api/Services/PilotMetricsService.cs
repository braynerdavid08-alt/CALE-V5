using Cale.BuildingBlocks.Domain.Assessment;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Classroom.Domain;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services;

public sealed class PilotMetricsService
{
    private readonly CaleDbContext _db;
    private readonly IClock _clock;

    public PilotMetricsService(CaleDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PilotMetricsDto> GetAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var day = now.AddDays(-1);
        var week = now.AddDays(-7);
        var month = now.AddDays(-30);
        var inactiveSince = now.AddDays(-14);

        var users = await _db.Set<User>().AsNoTracking().ToListAsync(ct);
        var students = users.Where(u => Roles.Normalize(u.Role) == Roles.Student).ToList();
        var teachers = users.Where(u => Roles.Normalize(u.Role) == Roles.Teacher).ToList();

        static bool ActiveSince(User u, DateTime from) =>
            u.IsActive && u.LastLoginAt is { } login && login >= from;

        var profiles = await _db.Set<SchoolProfile>().AsNoTracking().ToListAsync(ct);

        var activeSchools = profiles.Count(p =>
            p.SubscriptionStatus == SchoolSubscriptionStatus.Active
            && p.MembershipEndsAt is { } end
            && end > now);

        var pendingRequests = profiles.Count(p =>
            p.SubscriptionStatus is SchoolSubscriptionStatus.PendingPayment
                or SchoolSubscriptionStatus.UnderReview
                or SchoolSubscriptionStatus.PaymentSubmittedLegacy
            || (p.SubscriptionStatus == SchoolSubscriptionStatus.Active
                && (p.RenewalStatus is SchoolRenewalStatus.PendingPayment
                    or SchoolRenewalStatus.UnderReview
                    or SchoolRenewalStatus.Rejected))
            || (p.SubscriptionStatus == SchoolSubscriptionStatus.Active
                && !string.IsNullOrWhiteSpace(p.RequestedPlanCode)
                && (string.IsNullOrWhiteSpace(p.RenewalStatus)
                    || p.RenewalStatus == SchoolRenewalStatus.None)));

        var events = await _db.Set<MembershipEvent>()
            .AsNoTracking()
            .Where(x => x.CreatedAt >= month)
            .ToListAsync(ct);
        var requests30 = events.Count(x => x.EventType == MembershipEventTypes.Requested);
        var activations30 = events.Count(x =>
            x.EventType is MembershipEventTypes.Activated or MembershipEventTypes.Renewed);
        var conversion = requests30 == 0
            ? 0m
            : Math.Round(100m * activations30 / requests30, 1);

        var attempts = await _db.Set<Attempt>()
            .AsNoTracking()
            .Where(x => x.StartedAt >= month)
            .ToListAsync(ct);
        var started = attempts.Count;
        var finished = attempts.Count(x => x.FinishedAt != null);
        var examAttempts = attempts.Where(x => x.ExamId != null || x.Mode == AttemptModes.Exam)
            .ToList();
        var examStarted = examAttempts.Count;
        var examFinished = examAttempts.Count(x => x.FinishedAt != null);
        var examPassed = examAttempts.Count(x => x.FinishedAt != null && x.Passed);
        var abandoned = attempts.Count(x =>
            x.FinishedAt is null
            && x.ExpiresAt is { } exp
            && exp < now);
        var practice = attempts.Count(x => x.ExamId is null && x.Mode != AttemptModes.Exam);
        var simulatorShare = started == 0
            ? 0m
            : Math.Round(100m * practice / started, 1);

        var finishedExamTimes = examAttempts
            .Where(x => x.FinishedAt != null)
            .Select(x => x.TimeSeconds)
            .Where(t => t > 0)
            .ToList();
        var avgTime = finishedExamTimes.Count == 0
            ? 0m
            : Math.Round((decimal)finishedExamTimes.Average(), 1);

        var studentIds = students.Select(s => s.Id).ToHashSet();
        var attemptsPerStudent = studentIds.Count == 0
            ? 0m
            : Math.Round(
                (decimal)attempts.Count(a => studentIds.Contains(a.UserId))
                / studentIds.Count,
                2);

        var answers = await _db.Set<AttemptAnswer>().CountAsync(ct);
        var activeGroups = await _db.Set<Group>()
            .CountAsync(g => g.IsActive, ct);
        var submissions30 = await _db.Set<ActivitySubmission>()
            .CountAsync(s => s.SubmittedAt >= month, ct);

        var prevMonthStart = now.AddDays(-60);
        decimal Growth(int recent, int previous) =>
            previous <= 0
                ? (recent > 0 ? 100m : 0m)
                : Math.Round(100m * (recent - previous) / previous, 1);

        var usersRecent = users.Count(u => u.CreatedAt >= month);
        var usersPrev = users.Count(u => u.CreatedAt >= prevMonthStart && u.CreatedAt < month);
        var studentsRecent = students.Count(u => u.CreatedAt >= month);
        var studentsPrev = students.Count(u => u.CreatedAt >= prevMonthStart && u.CreatedAt < month);
        var teachersRecent = teachers.Count(u => u.CreatedAt >= month);
        var teachersPrev = teachers.Count(u => u.CreatedAt >= prevMonthStart && u.CreatedAt < month);
        var schoolsRecent = profiles.Count(p => p.CreatedAt >= month);
        var schoolsPrev = profiles.Count(p => p.CreatedAt >= prevMonthStart && p.CreatedAt < month);

        var seriesStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(-5);
        var monthly = new List<MonthlyRegistrationPointDto>(6);
        for (var i = 0; i < 6; i++)
        {
            var start = seriesStart.AddMonths(i);
            var end = start.AddMonths(1);
            monthly.Add(new MonthlyRegistrationPointDto(
                start.ToString("MMM", new System.Globalization.CultureInfo("es-CO")),
                start.Year,
                start.Month,
                students.Count(u => u.CreatedAt >= start && u.CreatedAt < end),
                teachers.Count(u => u.CreatedAt >= start && u.CreatedAt < end),
                profiles.Count(p => p.CreatedAt >= start && p.CreatedAt < end)));
        }

        return new PilotMetricsDto(
            users.Count(u => ActiveSince(u, day)),
            users.Count(u => ActiveSince(u, week)),
            users.Count(u => ActiveSince(u, month)),
            activeSchools,
            pendingRequests,
            requests30,
            activations30,
            conversion,
            students.Count,
            students.Count(s => ActiveSince(s, week)),
            students.Count(s =>
                s.IsActive
                && (s.LastLoginAt is null || s.LastLoginAt < inactiveSince)),
            teachers.Count,
            teachers.Count(t => ActiveSince(t, week)),
            activeGroups,
            started,
            finished,
            examStarted == 0
                ? 0m
                : Math.Round(100m * examFinished / examStarted, 1),
            examFinished == 0
                ? 0m
                : Math.Round(100m * examPassed / examFinished, 1),
            attemptsPerStudent,
            answers,
            avgTime,
            abandoned,
            simulatorShare,
            submissions30,
            Growth(usersRecent, usersPrev),
            Growth(schoolsRecent, schoolsPrev),
            Growth(teachersRecent, teachersPrev),
            Growth(studentsRecent, studentsPrev),
            monthly);
    }
}
