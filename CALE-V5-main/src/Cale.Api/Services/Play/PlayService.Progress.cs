using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Classroom;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Assessment.Domain.Gamification;
using Cale.Modules.Classroom.Domain;
using Cale.Modules.Identity.Domain;
using Cale.Modules.TheoreticalTraining.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cale.Api.Services.Play;

public sealed partial class PlayService
{
    // ───────────────────────── Stats, XP, levels ─────────────────────────

    public async Task<PlayStats> ComputeStatsAsync(int userId, CancellationToken ct) =>
        (await ComputeStatsCoreAsync(userId, ct)).Stats;

    private async Task<(PlayStats Stats, StreakDto Streak)> ComputeStatsCoreAsync(int userId, CancellationToken ct)
    {
        var attempts = await _db.Set<Attempt>().AsNoTracking()
            .Where(x => x.UserId == userId && x.FinishedAt != null)
            .Select(x => new { x.Passed, x.Percent, x.TotalQuestions })
            .ToListAsync(ct);
        var totalCorrect = await (
                from ans in _db.Set<AttemptAnswer>().AsNoTracking()
                join at in _db.Set<Attempt>().AsNoTracking() on ans.AttemptId equals at.Id
                where at.UserId == userId && at.FinishedAt != null && ans.IsCorrect
                select ans.Id)
            .CountAsync(ct);
        var daily = await _db.Set<DailyChallenge>().AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { Completed = x.CompletedAt != null, x.CorrectCount })
            .ToListAsync(ct);
        var mastered = await _db.Set<MistakeReview>().AsNoTracking()
            .CountAsync(x => x.UserId == userId && x.Mastered, ct);
        var games = await _db.Set<GameResult>().AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.Game, x.Score, x.Correct, x.Won })
            .ToListAsync(ct);
        var streak = await GetStreakAsync(userId, ct);

        var signs = games.Where(g => g.Game == GameKinds.Signs).ToList();
        var duels = games.Where(g => g.Game == GameKinds.Duel).ToList();

        var stats = new PlayStats(
            attempts.Count,
            attempts.Count(a => a.Passed),
            attempts.Count(a => a.Percent >= 100 && a.TotalQuestions >= 10),
            totalCorrect,
            streak.Best,
            daily.Count(d => d.Completed),
            daily.Sum(d => d.CorrectCount),
            mastered,
            signs.Select(s => s.Score).DefaultIfEmpty(0).Max(),
            signs.Sum(s => s.Correct),
            duels.Count(d => d.Won),
            duels.Count);
        return (stats, streak);
    }

    public static LevelDto BuildLevel(int xp)
    {
        var levels = BadgeCatalog.Levels;
        var index = 0;
        for (var i = 0; i < levels.Count; i++)
        {
            if (xp >= levels[i].Xp)
            {
                index = i;
            }
        }

        var start = levels[index].Xp;
        int? next = index + 1 < levels.Count ? levels[index + 1].Xp : null;
        var progress = next is { } n ? (int)Math.Round(100.0 * (xp - start) / Math.Max(1, n - start)) : 100;
        return new LevelDto(
            index + 1,
            levels[index].Name,
            xp,
            start,
            next,
            index + 1 < levels.Count ? levels[index + 1].Name : null,
            Math.Clamp(progress, 0, 100));
    }

    public async Task<AchievementsDto> GetAchievementsAsync(int userId, CancellationToken ct)
    {
        var stats = await ComputeStatsAsync(userId, ct);
        var newBadges = await CheckAchievementsAsync(userId, stats, ct);
        var earned = await _db.Set<UserAchievement>().AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToDictionaryAsync(x => x.Code, x => x.EarnedAt, ct);

        var badges = BadgeCatalog.All
            .Select(b => ToBadge(b, stats, earned.TryGetValue(b.Code, out var at) ? at : null))
            .OrderByDescending(b => b.Earned)
            .ThenByDescending(b => b.Target == 0 ? 0 : (double)b.Current / b.Target)
            .ToList();

        return new AchievementsDto(BuildLevel(stats.Xp), badges, newBadges);
    }

    /// <summary>Persists newly reached badges, notifies the student and returns them.</summary>
    public Task<IReadOnlyList<BadgeDto>> CheckAchievementsAsync(int userId, CancellationToken ct) =>
        CheckAchievementsAsync(userId, null, ct);

    private async Task<IReadOnlyList<BadgeDto>> CheckAchievementsAsync(
        int userId,
        PlayStats? knownStats,
        CancellationToken ct)
    {
        var stats = knownStats ?? await ComputeStatsAsync(userId, ct);
        var owned = await _db.Set<UserAchievement>().AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.Code)
            .ToListAsync(ct);

        var now = _clock.UtcNow;
        var reached = BadgeCatalog.All
            .Where(b => !owned.Contains(b.Code) && b.Current(stats) >= b.Target)
            .ToList();
        if (reached.Count == 0)
        {
            return [];
        }

        foreach (var badge in reached)
        {
            _db.Set<UserAchievement>().Add(new UserAchievement
            {
                UserId = userId,
                Code = badge.Code,
                EarnedAt = now
            });
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another request stored them first; do not celebrate twice.
            foreach (var entry in _db.ChangeTracker.Entries<UserAchievement>().ToList())
            {
                entry.State = EntityState.Detached;
            }

            return [];
        }

        foreach (var badge in reached)
        {
            await _notifications.NotifyUsersAsync(
                [userId],
                new NotificationDraft(
                    $"Nueva insignia: {badge.Title}",
                    badge.Description,
                    NotificationTypes.Achievement,
                    Link: "/student/play/achievements",
                    DedupeKey: $"badge:{userId}:{badge.Code}"),
                ct);
        }

        return reached.Select(b => ToBadge(b, stats, now)).ToList();
    }

    private static BadgeDto ToBadge(BadgeDef def, PlayStats stats, DateTime? earnedAt) =>
        new(
            def.Code,
            def.Title,
            def.Description,
            def.Icon,
            earnedAt is not null,
            earnedAt,
            Math.Min(def.Current(stats), def.Target),
            def.Target);

    // ───────────────────────── Weekly ranking ─────────────────────────

    public async Task<RankingDto> GetRankingAsync(
        int userId,
        string? scope,
        int? groupId,
        CancellationToken ct)
    {
        var me = await _db.Set<User>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new Cale.BuildingBlocks.Domain.Exceptions.NotFoundException("User not found.");

        var myGroupIds = await _db.Set<GroupMember>().AsNoTracking()
            .Where(m => m.UserId == userId && (m.Status == MemberStatuses.Active || m.Status == "Active"))
            .Select(m => m.GroupId)
            .ToListAsync(ct);
        var groups = await _db.Set<Group>().AsNoTracking()
            .Where(g => myGroupIds.Contains(g.Id) && g.IsActive)
            .Select(g => new { g.Id, g.Name })
            .ToListAsync(ct);

        var scopes = new List<RankingScopeDto>();
        if (me.SchoolId is not null)
        {
            scopes.Add(new RankingScopeDto("school", "Mi escuela", null));
        }

        scopes.AddRange(groups.Select(g => new RankingScopeDto("group", g.Name, g.Id)));
        scopes.Add(new RankingScopeDto("global", "Todo Luz Verde", null));

        var resolved = scope?.ToLowerInvariant() switch
        {
            "school" when me.SchoolId is not null => "school",
            "group" when groups.Count > 0 => "group",
            "global" => "global",
            _ => me.SchoolId is not null ? "school" : groups.Count > 0 ? "group" : "global"
        };
        var selectedGroup = resolved == "group"
            ? groups.FirstOrDefault(g => g.Id == groupId) ?? groups[0]
            : null;

        var weekStart = WeekStart();
        var boardKey = resolved switch
        {
            "school" => $"play:board:school:{me.SchoolId}:{weekStart:yyyyMMdd}",
            "group" => $"play:board:group:{selectedGroup?.Id}:{weekStart:yyyyMMdd}",
            _ => $"play:board:global:{weekStart:yyyyMMdd}"
        };
        if (!_cache.TryGetValue(boardKey, out List<BoardRow>? cachedBoard) || cachedBoard is null)
        {
            cachedBoard = await LoadBoardAsync(resolved, me.SchoolId, selectedGroup?.Id, weekStart, ct);
            CacheSmall(boardKey, cachedBoard, TimeSpan.FromSeconds(60));
        }

        var myXp = (await ComputeWeeklyXpAsync([userId], weekStart, ct)).GetValueOrDefault(userId);
        var students = cachedBoard
            .Where(s => s.Id != userId)
            .Append(new BoardRow(userId, me.Name, myXp))
            .ToList();
        var xp = students.ToDictionary(s => s.Id, s => s.Xp);

        var hidden = await _db.Set<PlayerProfile>().AsNoTracking()
            .Where(p => !p.ShowInRanking)
            .Select(p => p.UserId)
            .ToListAsync(ct);
        var myVisible = !hidden.Contains(userId);

        var board = students
            .Where(s => !hidden.Contains(s.Id))
            .Select(s => new { s.Id, s.Name, Xp = xp.GetValueOrDefault(s.Id) })
            .Where(s => s.Xp > 0 || s.Id == userId)
            .OrderByDescending(s => s.Xp)
            .ThenBy(s => s.Name)
            .ToList();

        var entries = board
            .Select((s, i) => new RankingEntryDto(i + 1, s.Id, PublicName(s.Name), s.Xp, s.Id == userId))
            .ToList();
        var mine = entries.FirstOrDefault(e => e.IsMe);

        var label = resolved switch
        {
            "school" => "Mi escuela",
            "group" => selectedGroup?.Name ?? "Mi grupo",
            _ => "Todo Luz Verde"
        };

        return new RankingDto(
            resolved,
            label,
            selectedGroup?.Id,
            weekStart,
            weekStart.AddDays(6),
            entries.Take(20).Concat(mine is { Position: > 20 } ? [mine] : []).ToList(),
            mine?.Position,
            xp.GetValueOrDefault(userId),
            entries.Count,
            myVisible,
            scopes);
    }

    private async Task<List<BoardRow>> LoadBoardAsync(
        string scope,
        int? schoolId,
        int? groupId,
        DateOnly weekStart,
        CancellationToken ct)
    {
        var query = _db.Set<User>().AsNoTracking().Where(u => u.IsActive);
        if (scope == "school")
        {
            query = query.Where(u => u.SchoolId == schoolId);
        }
        else if (scope == "group" && groupId is not null)
        {
            var memberIds = await _db.Set<GroupMember>().AsNoTracking()
                .Where(m => m.GroupId == groupId && (m.Status == MemberStatuses.Active || m.Status == "Active"))
                .Select(m => m.UserId)
                .ToListAsync(ct);
            query = query.Where(u => memberIds.Contains(u.Id));
        }

        var students = (await query
                .Select(u => new { u.Id, u.Name, u.Role })
                .ToListAsync(ct))
            .Where(u => Roles.Normalize(u.Role) == Roles.Student)
            .ToList();
        var xp = await ComputeWeeklyXpAsync(students.Select(s => s.Id).ToList(), weekStart, ct);
        return students
            .Select(s => new BoardRow(s.Id, s.Name, xp.GetValueOrDefault(s.Id)))
            .Where(s => s.Xp > 0)
            .ToList();
    }

    private sealed record BoardRow(int Id, string Name, int Xp);

    public async Task SetRankingVisibilityAsync(int userId, bool show, CancellationToken ct)
    {
        var row = await _db.Set<PlayerProfile>().FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (row is null)
        {
            _db.Set<PlayerProfile>().Add(new PlayerProfile
            {
                UserId = userId,
                ShowInRanking = show,
                UpdatedAt = _clock.UtcNow
            });
        }
        else
        {
            row.ShowInRanking = show;
            row.UpdatedAt = _clock.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<Dictionary<int, int>> ComputeWeeklyXpAsync(
        IReadOnlyCollection<int> userIds,
        DateOnly weekStart,
        CancellationToken ct)
    {
        var result = new Dictionary<int, int>();
        if (userIds.Count == 0)
        {
            return result;
        }

        var sinceUtc = ColombiaTime.StartOfDayUtc(weekStart);
        void Add(int user, int value)
        {
            if (value > 0)
            {
                result[user] = result.GetValueOrDefault(user) + value;
            }
        }

        var correct = await (
                from ans in _db.Set<AttemptAnswer>().AsNoTracking()
                join at in _db.Set<Attempt>().AsNoTracking() on ans.AttemptId equals at.Id
                where userIds.Contains(at.UserId) && at.FinishedAt >= sinceUtc && ans.IsCorrect
                group ans by at.UserId into g
                select new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        foreach (var r in correct)
        {
            Add(r.UserId, r.Count * XpRules.AttemptCorrect);
        }

        var passed = await _db.Set<Attempt>().AsNoTracking()
            .Where(a => userIds.Contains(a.UserId) && a.FinishedAt >= sinceUtc && a.Passed)
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        foreach (var r in passed)
        {
            Add(r.UserId, r.Count * XpRules.AttemptPassed);
        }

        var daily = await _db.Set<DailyChallenge>().AsNoTracking()
            .Where(d => userIds.Contains(d.UserId) && d.ChallengeDate >= weekStart)
            .Select(d => new { d.UserId, Completed = d.CompletedAt != null, d.CorrectCount })
            .ToListAsync(ct);
        foreach (var r in daily)
        {
            Add(r.UserId, (r.Completed ? XpRules.DailyCompleted : 0) + r.CorrectCount * XpRules.DailyCorrect);
        }

        var mastered = await _db.Set<MistakeReview>().AsNoTracking()
            .Where(m => userIds.Contains(m.UserId) && m.Mastered && m.UpdatedAt >= sinceUtc)
            .GroupBy(m => m.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        foreach (var r in mastered)
        {
            Add(r.UserId, r.Count * XpRules.MistakeMastered);
        }

        var games = await _db.Set<GameResult>().AsNoTracking()
            .Where(g => userIds.Contains(g.UserId) && g.PlayedAt >= sinceUtc)
            .Select(g => new { g.UserId, g.Game, g.Correct, g.Won })
            .ToListAsync(ct);
        foreach (var r in games)
        {
            Add(r.UserId, r.Game == GameKinds.Duel
                ? XpRules.DuelPlayed + (r.Won ? XpRules.DuelWon : 0)
                : r.Correct * XpRules.SignCorrect);
        }

        return result;
    }
}
