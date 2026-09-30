using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Classroom;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Assessment.Domain.Gamification;
using Cale.Modules.Classroom.Domain;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Play;

public sealed partial class PlayService
{
    public async Task<IReadOnlyList<DuelQuestion>> BuildDuelQuestionsAsync(CancellationToken ct)
    {
        var pool = await OfficialQuestionIdsAsync(ct);
        var seed = Random.Shared.Next();
        var picked = pool
            .OrderBy(id => StableHash(seed, id))
            .Take(DuelService.QuestionCount)
            .ToList();
        var questions = await LoadQuestionsAsync(picked, ct);
        return picked
            .Where(questions.ContainsKey)
            .Select(id => new DuelQuestion(
                ToDto(questions[id], seed),
                CorrectOptionId(questions[id]),
                questions[id].Explanation))
            .ToList();
    }

    public async Task<string> GetDisplayNameAsync(int userId, CancellationToken ct)
    {
        var name = await _db.Set<User>().AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync(ct);
        return PublicName(name ?? "");
    }

    // ───────────────────────── Inactive students (staff) ─────────────────────────

    public async Task<IReadOnlyList<InactiveStudentDto>> GetInactiveStudentsAsync(
        int actorId,
        string role,
        int days,
        CancellationToken ct)
    {
        days = Math.Clamp(days, 1, 365);
        var students = await ScopedStudentsAsync(actorId, role, ct);
        if (students.Count == 0)
        {
            return [];
        }

        return (await InactiveCoreAsync(students, days, ct)).Take(200).ToList();
    }

    /// <summary>
    /// Once a day (Colombia evening) nudges students that have not practised in the last day.
    /// Only accounts that logged in at least once and went quiet less than 60 days ago.
    /// </summary>
    public async Task<int> SendAutoNudgesAsync(CancellationToken ct)
    {
        var students = await ScopedStudentsAsync(0, Roles.Admin, ct);
        if (students.Count == 0)
        {
            return 0;
        }

        var inactive = (await InactiveCoreAsync(students, 1, ct))
            .Where(s => s.LastActivityAt is not null && s.DaysInactive is >= 1 and <= 60)
            .ToList();
        var today = DateOnly.FromDateTime(_clock.UtcNow.AddHours(ColombiaUtcOffsetHours));
        var day = today.ToString("yyyyMMdd");

        foreach (var s in inactive)
        {
            var (title, body) = PlayNudges.ForInactive(s.UserId, today, s.DaysInactive);
            await _notifications.NotifyUsersAsync(
                [s.UserId],
                new NotificationDraft(
                    title,
                    body,
                    NotificationTypes.Reminder,
                    Link: "/student/play/daily",
                    Priority: NotificationPriorities.High,
                    DedupeKey: $"nudge:{s.UserId}:{day}"),
                ct);
        }

        return inactive.Count;
    }

    private async Task<List<InactiveStudentDto>> InactiveCoreAsync(
        List<ScopedStudent> students,
        int days,
        CancellationToken ct)
    {
        var ids = students.Select(s => s.Id).ToList();
        var lastActivity = students.ToDictionary(s => s.Id, s => s.LastLoginAt);
        void Bump(int id, DateTime? at)
        {
            if (at is { } t && (lastActivity[id] is null || t > lastActivity[id]))
            {
                lastActivity[id] = t;
            }
        }

        foreach (var r in await _db.Set<Attempt>().AsNoTracking()
                     .Where(a => ids.Contains(a.UserId))
                     .GroupBy(a => a.UserId)
                     .Select(g => new { UserId = g.Key, At = g.Max(a => a.StartedAt) })
                     .ToListAsync(ct))
        {
            Bump(r.UserId, r.At);
        }

        foreach (var r in await _db.Set<DailyChallenge>().AsNoTracking()
                     .Where(d => ids.Contains(d.UserId))
                     .GroupBy(d => d.UserId)
                     .Select(g => new { UserId = g.Key, At = g.Max(d => d.CreatedAt) })
                     .ToListAsync(ct))
        {
            Bump(r.UserId, r.At);
        }

        foreach (var r in await _db.Set<GameResult>().AsNoTracking()
                     .Where(g => ids.Contains(g.UserId))
                     .GroupBy(g => g.UserId)
                     .Select(g => new { UserId = g.Key, At = g.Max(x => x.PlayedAt) })
                     .ToListAsync(ct))
        {
            Bump(r.UserId, r.At);
        }

        var now = _clock.UtcNow;
        var cutoff = now.AddDays(-days);
        return students
            .Select(s =>
            {
                var at = lastActivity[s.Id];
                return new InactiveStudentDto(
                    s.Id,
                    s.Name,
                    s.Email,
                    at,
                    at is { } t ? (int)Math.Floor((now - t).TotalDays) : null);
            })
            .Where(s => s.LastActivityAt is null || s.LastActivityAt < cutoff)
            .OrderBy(s => s.LastActivityAt is null ? 0 : 1)
            .ThenBy(s => s.LastActivityAt)
            .ToList();
    }

    public async Task<int> RemindStudentsAsync(
        int actorId,
        string role,
        IReadOnlyList<int> userIds,
        CancellationToken ct)
    {
        if (userIds.Count == 0)
        {
            return 0;
        }

        var allowed = (await ScopedStudentsAsync(actorId, role, ct)).Select(s => s.Id).ToHashSet();
        var targets = userIds.Where(allowed.Contains).Distinct().Take(200).ToList();
        if (targets.Count == 0)
        {
            throw new ForbiddenException("None of those students are under your supervision.");
        }

        var actorName = await _db.Set<User>().AsNoTracking()
            .Where(u => u.Id == actorId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync(ct) ?? "Tu escuela";
        var day = _clock.UtcNow.ToString("yyyyMMdd");
        var today = DateOnly.FromDateTime(_clock.UtcNow.AddHours(ColombiaUtcOffsetHours));

        foreach (var id in targets)
        {
            var (title, body) = PlayNudges.FromActor(id, today, PublicName(actorName));
            await _notifications.NotifyUsersAsync(
                [id],
                new NotificationDraft(
                    title,
                    body,
                    NotificationTypes.Reminder,
                    Link: "/student/play/daily",
                    Priority: NotificationPriorities.High,
                    DedupeKey: $"reminder:{id}:{day}"),
                ct);
        }

        return targets.Count;
    }

    private async Task<List<ScopedStudent>> ScopedStudentsAsync(int actorId, string role, CancellationToken ct)
    {
        var users = (await _db.Set<User>().AsNoTracking()
                .Where(u => u.IsActive)
                .Select(u => new { u.Id, u.Name, u.Email, u.Role, u.SchoolId, u.LastLoginAt })
                .ToListAsync(ct))
            .Where(u => Roles.Normalize(u.Role) == Roles.Student)
            .ToList();

        IEnumerable<int> allowedIds;
        switch (Roles.Normalize(role))
        {
            case Roles.Admin:
                allowedIds = users.Select(u => u.Id);
                break;
            case Roles.School:
                allowedIds = users.Where(u => u.SchoolId == actorId).Select(u => u.Id);
                break;
            case Roles.Teacher:
                var groupIds = await _db.Set<Group>().AsNoTracking()
                    .Where(g => g.TeacherId == actorId && g.IsActive)
                    .Select(g => g.Id)
                    .ToListAsync(ct);
                var memberIds = await _db.Set<GroupMember>().AsNoTracking()
                    .Where(m => groupIds.Contains(m.GroupId)
                        && (m.Status == MemberStatuses.Active || m.Status == "Active"))
                    .Select(m => m.UserId)
                    .ToListAsync(ct);
                var teacherSchool = await _db.Set<User>().AsNoTracking()
                    .Where(u => u.Id == actorId)
                    .Select(u => u.SchoolId)
                    .FirstOrDefaultAsync(ct);
                allowedIds = memberIds.Concat(teacherSchool is null
                    ? []
                    : users.Where(u => u.SchoolId == teacherSchool).Select(u => u.Id));
                break;
            default:
                throw new ForbiddenException("Only staff can see inactive students.");
        }

        var allowed = allowedIds.ToHashSet();
        return users
            .Where(u => allowed.Contains(u.Id))
            .Select(u => new ScopedStudent(u.Id, u.Name, u.Email, u.LastLoginAt))
            .ToList();
    }

    internal const int ColombiaUtcOffsetHours = -5;

    private sealed record ScopedStudent(int Id, string Name, string Email, DateTime? LastLoginAt);
}
