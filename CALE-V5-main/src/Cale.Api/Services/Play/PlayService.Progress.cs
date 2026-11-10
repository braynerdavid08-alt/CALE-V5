using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Classroom;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Assessment.Domain.Gamification;
using Cale.Modules.Classroom.Domain;
using Cale.Modules.Courses.Application;
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
        var courses = await CourseService.CompletionCountsAsync(_db, userId, ct);

        var signs = games.Where(g => g.Game == GameKinds.Signs).ToList();
        var duels = games.Where(g => g.Game == GameKinds.Duel).ToList();
        var tournaments = games.Where(g => g.Game == GameKinds.Tournament).ToList();
        var championships = await _db.Set<TournamentTrophy>().AsNoTracking()
            .CountAsync(t => t.UserId == userId, ct);

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
            duels.Count,
            courses.Lessons,
            courses.Courses,
            championships,
            tournaments.Count(t => t.Won),
            tournaments.Count);
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

    // ───────────────────────── Ranking (historical XP / trophies) ─────────────────────────

    public Task<RankingDto> GetRankingAsync(
        int userId,
        string? scope,
        int? groupId,
        CancellationToken ct) =>
        GetRankingAsync(userId, scope, groupId, "all", "xp", ct);

    public async Task<RankingDto> GetRankingAsync(
        int userId,
        string? scope,
        int? groupId,
        string? period,
        string? metric,
        CancellationToken ct)
    {
        var me = await _db.Set<User>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new Cale.BuildingBlocks.Domain.Exceptions.NotFoundException("User not found.");

        var myGroupIds = await _db.Set<GroupMember>().AsNoTracking()
            .Where(m => m.UserId == userId && (m.Status == MemberStatuses.Active || m.Status == "Active"))
            .Select(m => m.GroupId)
            .ToListAsync(ct);
        var activeGroupIds = await _db.Set<Group>().AsNoTracking()
            .Where(g => myGroupIds.Contains(g.Id) && g.IsActive)
            .Select(g => g.Id)
            .OrderBy(id => id)
            .ToListAsync(ct);
        var hasGroups = activeGroupIds.Count > 0;

        // Single "Mi grupo" chip: union of all active memberships (no per-class buttons).
        var scopes = new List<RankingScopeDto>();
        if (me.SchoolId is not null)
        {
            scopes.Add(new RankingScopeDto("school", "Mi escuela", null));
        }

        if (hasGroups)
        {
            scopes.Add(new RankingScopeDto("group", "Mi grupo", null));
        }

        scopes.Add(new RankingScopeDto("global", "Todo Luz Verde", null));

        var resolved = scope?.ToLowerInvariant() switch
        {
            "school" when me.SchoolId is not null => "school",
            "group" when hasGroups => "group",
            "global" => "global",
            _ => me.SchoolId is not null ? "school" : hasGroups ? "group" : "global"
        };

        // Period UI removed: always historical cumulative. groupId query is ignored (union).
        _ = groupId;
        var periodKey = NormalizePeriodKey(period);
        var (periodStart, periodEnd) = ResolvePeriod(periodKey);
        var metricKey = NormalizeMetricKey(metric);

        var groupUnionKey = hasGroups ? string.Join(',', activeGroupIds) : "";
        var holdScopeKey = resolved switch
        {
            "school" => $"school:{me.SchoolId}",
            "group" => $"group:union:{groupUnionKey}",
            _ => "global"
        };
        var boardKey = $"play:board:{holdScopeKey}:{periodKey}:{periodStart:yyyyMMdd}:{metricKey}";
        if (!_cache.TryGetValue(boardKey, out List<CompetitiveRow>? cachedBoard) || cachedBoard is null)
        {
            cachedBoard = await LoadCompetitiveBoardAsync(
                resolved,
                me.SchoolId,
                resolved == "group" ? activeGroupIds : null,
                periodStart,
                periodEnd,
                ct);
            CacheSmall(boardKey, cachedBoard, TimeSpan.FromMinutes(2));
        }

        var myXp = (await ComputePeriodXpAsync([userId], periodStart, periodEnd, ct)).GetValueOrDefault(userId);
        var myTrophies = await CountTrophiesAsync(userId, periodStart, periodEnd, ct);
        var myAchievements = await CountAchievementsAsync(userId, periodStart, periodEnd, ct);
        var students = cachedBoard
            .Where(s => s.Id != userId)
            .Append(new CompetitiveRow(userId, me.Name, myXp, myTrophies, myAchievements, me.PhotoUrl, null, null))
            .ToList();

        var hidden = await _db.Set<PlayerProfile>().AsNoTracking()
            .Where(p => !p.ShowInRanking)
            .Select(p => p.UserId)
            .ToListAsync(ct);
        var myVisible = !hidden.Contains(userId);

        var visible = students.Where(s => !hidden.Contains(s.Id)).ToList();
        var entries = RankCompetitive(visible, userId, metricKey, PublicName);

        Dictionary<int, DateTime> heldSince = [];
        if (metricKey == "xp")
        {
            var forHolds = entries
                .Select(e => (e.Position, e.UserId, e.DisplayName, e.Xp, e.PhotoUrl, e.IsMe))
                .ToList();
            heldSince = await SyncRankingHoldsAsync(holdScopeKey, forHolds, ct);
        }

        // Never invent DateTime.MinValue holds (frontend "2055 años" bug).
        entries = entries
            .Select(e =>
            {
                DateTime? since = null;
                if (metricKey == "xp" && heldSince.TryGetValue(e.UserId, out var held) && held.Year >= 2000)
                {
                    since = held;
                }

                return e with { PositionHeldSinceUtc = since };
            })
            .ToList();
        var mine = entries.FirstOrDefault(e => e.IsMe);

        var label = resolved switch
        {
            "school" => "Mi escuela",
            "group" => "Mi grupo",
            _ => "Todo Luz Verde"
        };

        return new RankingDto(
            resolved,
            label,
            null,
            periodStart,
            periodEnd,
            entries.Take(20).Concat(mine is { Position: > 20 } ? [mine] : []).ToList(),
            mine?.Position,
            myXp,
            entries.Count,
            myVisible,
            scopes,
            metricKey,
            periodKey,
            RankingWeightsV1.Version,
            myTrophies,
            myAchievements,
            mine?.GeneralScore,
            Array.Empty<RankingPeriodDto>());
    }

    private static string NormalizePeriodKey(string? period)
    {
        // Date filters removed from UI; always serve historical cumulative totals.
        _ = period;
        return "all";
    }

    private static string NormalizeMetricKey(string? metric) =>
        metric?.ToLowerInvariant() switch
        {
            "trophies" => "trophies",
            // Legacy UI keys map to XP; achievements stay on profiles, not ranking tabs.
            "general" or "achievements" or "xp" => "xp",
            _ => "xp"
        };

    private static (DateOnly Start, DateOnly End) ResolvePeriod(string periodKey)
    {
        var end = ColombiaTime.TodayInColombia();
        var start = periodKey switch
        {
            "30d" => end.AddDays(-30),
            "90d" => end.AddDays(-90),
            "6m" => end.AddMonths(-6),
            "1y" => end.AddYears(-1),
            "all" => new DateOnly(2000, 1, 1),
            _ => end.AddYears(-2)
        };
        return (start, end);
    }

    private async Task<int> CountTrophiesAsync(int userId, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var since = ColombiaTime.StartOfDayUtc(start);
        var until = ColombiaTime.StartOfDayUtc(end.AddDays(1));
        return await _db.Set<TournamentTrophy>().AsNoTracking()
            .CountAsync(t => t.UserId == userId && t.ObtainedAt >= since && t.ObtainedAt < until, ct);
    }

    private async Task<int> CountAchievementsAsync(int userId, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var since = ColombiaTime.StartOfDayUtc(start);
        var until = ColombiaTime.StartOfDayUtc(end.AddDays(1));
        return await _db.Set<UserAchievement>().AsNoTracking()
            .CountAsync(a => a.UserId == userId && a.EarnedAt >= since && a.EarnedAt < until, ct);
    }

    private async Task<List<CompetitiveRow>> LoadCompetitiveBoardAsync(
        string scope,
        int? schoolId,
        IReadOnlyList<int>? groupIds,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken ct)
    {
        var xpBoard = await LoadBoardAsync(scope, schoolId, groupIds, periodStart, periodEnd, ct);
        var ids = xpBoard.Select(x => x.Id).ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var since = ColombiaTime.StartOfDayUtc(periodStart);
        var until = ColombiaTime.StartOfDayUtc(periodEnd.AddDays(1));
        var trophies = await _db.Set<TournamentTrophy>().AsNoTracking()
            .Where(t => ids.Contains(t.UserId) && t.ObtainedAt >= since && t.ObtainedAt < until)
            .GroupBy(t => t.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Count = g.Count(),
                Last = g.OrderByDescending(x => x.ObtainedAt).Select(x => x.TournamentNameSnapshot).FirstOrDefault()
            })
            .ToListAsync(ct);
        var trophyMap = trophies.ToDictionary(x => x.UserId, x => (x.Count, x.Last));

        var achievements = await _db.Set<UserAchievement>().AsNoTracking()
            .Where(a => ids.Contains(a.UserId) && a.EarnedAt >= since && a.EarnedAt < until)
            .GroupBy(a => a.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Count = g.Count(),
                Last = g.OrderByDescending(x => x.EarnedAt).Select(x => x.Code).FirstOrDefault()
            })
            .ToListAsync(ct);
        var achievementMap = achievements.ToDictionary(x => x.UserId, x => (x.Count, x.Last));

        return xpBoard.Select(r =>
        {
            trophyMap.TryGetValue(r.Id, out var t);
            achievementMap.TryGetValue(r.Id, out var a);
            return new CompetitiveRow(r.Id, r.Name, r.Xp, t.Count, a.Count, r.PhotoUrl, t.Last, a.Last);
        }).ToList();
    }

    private static List<RankingEntryDto> RankCompetitive(
        List<CompetitiveRow> rows,
        int viewerId,
        string metricKey,
        Func<string, string> publicName)
    {
        var withPresence = rows
            .Where(r => r.Id == viewerId || r.Xp > 0 || r.Trophies > 0 || r.Achievements > 0)
            .ToList();

        if (metricKey == "general")
        {
            var xpSorted = withPresence.OrderByDescending(r => r.Xp).Select(r => r.Xp).ToList();
            var trophySorted = withPresence.OrderByDescending(r => r.Trophies).Select(r => r.Trophies).ToList();
            var achSorted = withPresence.OrderByDescending(r => r.Achievements).Select(r => r.Achievements).ToList();
            var scored = withPresence.Select(r =>
            {
                var xpIdx = xpSorted.FindIndex(v => v == r.Xp);
                var tIdx = trophySorted.FindIndex(v => v == r.Trophies);
                var aIdx = achSorted.FindIndex(v => v == r.Achievements);
                var score = RankingWeightsV1.GeneralIndex(
                    RankingWeightsV1.Percentile(Math.Max(0, xpIdx), xpSorted.Count, xpSorted),
                    RankingWeightsV1.Percentile(Math.Max(0, tIdx), trophySorted.Count, trophySorted),
                    RankingWeightsV1.Percentile(Math.Max(0, aIdx), achSorted.Count, achSorted));
                return (Row: r, Score: score);
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Row.Xp)
            .ThenBy(x => x.Row.Name)
            .Select((x, i) => new RankingEntryDto(
                i + 1,
                x.Row.Id,
                publicName(x.Row.Name),
                x.Row.Xp,
                x.Row.Id == viewerId,
                x.Row.PhotoUrl,
                null,
                x.Row.Trophies,
                x.Row.Achievements,
                Math.Round(x.Score, 2),
                x.Row.LastTrophy,
                x.Row.LastAchievement))
            .ToList();
            return scored;
        }

        Func<CompetitiveRow, int> key = metricKey switch
        {
            "trophies" => r => r.Trophies,
            "achievements" => r => r.Achievements,
            _ => r => r.Xp
        };

        return withPresence
            .OrderByDescending(key)
            .ThenByDescending(r => r.Xp)
            .ThenBy(r => r.Name)
            .Select((r, i) => new RankingEntryDto(
                i + 1,
                r.Id,
                publicName(r.Name),
                r.Xp,
                r.Id == viewerId,
                r.PhotoUrl,
                null,
                r.Trophies,
                r.Achievements,
                null,
                r.LastTrophy,
                r.LastAchievement))
            .ToList();
    }

    private sealed record CompetitiveRow(
        int Id,
        string Name,
        int Xp,
        int Trophies,
        int Achievements,
        string? PhotoUrl,
        string? LastTrophy,
        string? LastAchievement);

    private async Task<List<BoardRow>> LoadBoardAsync(
        string scope,
        int? schoolId,
        IReadOnlyList<int>? groupIds,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken ct)
    {
        var query = _db.Set<User>().AsNoTracking().Where(u => u.IsActive);
        if (scope == "school")
        {
            query = query.Where(u => u.SchoolId == schoolId);
        }
        else if (scope == "group" && groupIds is { Count: > 0 })
        {
            // Union of all active group memberships; Distinct via Contains on user ids.
            var memberIds = await _db.Set<GroupMember>().AsNoTracking()
                .Where(m => groupIds.Contains(m.GroupId) && (m.Status == MemberStatuses.Active || m.Status == "Active"))
                .Select(m => m.UserId)
                .Distinct()
                .ToListAsync(ct);
            query = query.Where(u => memberIds.Contains(u.Id));
        }

        var students = (await query
                .Select(u => new { u.Id, u.Name, u.Role, u.PhotoUrl })
                .ToListAsync(ct))
            .Where(u => Roles.Normalize(u.Role) == Roles.Student)
            .GroupBy(u => u.Id)
            .Select(g => g.First())
            .ToList();
        var xp = await ComputePeriodXpAsync(students.Select(s => s.Id).ToList(), periodStart, periodEnd, ct);
        return students
            .Select(s => new BoardRow(s.Id, s.Name, xp.GetValueOrDefault(s.Id), s.PhotoUrl))
            .Where(s => s.Xp > 0)
            .ToList();
    }

    private sealed record BoardRow(int Id, string Name, int Xp, string? PhotoUrl);

    /// <summary>
    /// Persists when each student entered their current rank slot for this scope.
    /// Same position → keep HeldSince; position change → reset to now.
    /// </summary>
    private async Task<Dictionary<int, DateTime>> SyncRankingHoldsAsync(
        string scopeKey,
        IReadOnlyList<(int Position, int Id, string Name, int Xp, string? PhotoUrl, bool IsMe)> ranked,
        CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var ids = ranked.Select(r => r.Id).ToList();
        var existing = await _db.Set<RankingPositionHold>()
            .Where(h => h.ScopeKey == scopeKey && ids.Contains(h.UserId))
            .ToListAsync(ct);
        var byUser = existing.ToDictionary(h => h.UserId);
        var result = new Dictionary<int, DateTime>();

        foreach (var row in ranked)
        {
            if (byUser.TryGetValue(row.Id, out var hold))
            {
                if (hold.Position == row.Position)
                {
                    hold.Xp = row.Xp;
                    result[row.Id] = hold.HeldSinceUtc;
                }
                else
                {
                    hold.Position = row.Position;
                    hold.Xp = row.Xp;
                    hold.HeldSinceUtc = now;
                    result[row.Id] = now;
                }
            }
            else
            {
                _db.Set<RankingPositionHold>().Add(new RankingPositionHold
                {
                    ScopeKey = scopeKey,
                    UserId = row.Id,
                    Position = row.Position,
                    Xp = row.Xp,
                    HeldSinceUtc = now
                });
                result[row.Id] = now;
            }
        }

        // Drop stale holds for users no longer on this board (keeps table small).
        var stale = await _db.Set<RankingPositionHold>()
            .Where(h => h.ScopeKey == scopeKey && !ids.Contains(h.UserId))
            .ToListAsync(ct);
        if (stale.Count > 0)
        {
            _db.Set<RankingPositionHold>().RemoveRange(stale);
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // Ranking must still render if hold table is missing mid-deploy.
        }

        return result;
    }

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

    private async Task<Dictionary<int, int>> ComputePeriodXpAsync(
        IReadOnlyCollection<int> userIds,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken ct)
    {
        var result = new Dictionary<int, int>();
        if (userIds.Count == 0)
        {
            return result;
        }

        var sinceUtc = ColombiaTime.StartOfDayUtc(periodStart);
        var untilUtc = ColombiaTime.StartOfDayUtc(periodEnd.AddDays(1));
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
                where userIds.Contains(at.UserId)
                    && at.FinishedAt >= sinceUtc
                    && at.FinishedAt < untilUtc
                    && ans.IsCorrect
                group ans by at.UserId into g
                select new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        foreach (var r in correct)
        {
            Add(r.UserId, r.Count * XpRules.AttemptCorrect);
        }

        var passed = await _db.Set<Attempt>().AsNoTracking()
            .Where(a => userIds.Contains(a.UserId)
                && a.FinishedAt >= sinceUtc
                && a.FinishedAt < untilUtc
                && a.Passed)
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        foreach (var r in passed)
        {
            Add(r.UserId, r.Count * XpRules.AttemptPassed);
        }

        var daily = await _db.Set<DailyChallenge>().AsNoTracking()
            .Where(d => userIds.Contains(d.UserId)
                && d.ChallengeDate >= periodStart
                && d.ChallengeDate <= periodEnd)
            .Select(d => new { d.UserId, Completed = d.CompletedAt != null, d.CorrectCount })
            .ToListAsync(ct);
        foreach (var r in daily)
        {
            Add(r.UserId, (r.Completed ? XpRules.DailyCompleted : 0) + r.CorrectCount * XpRules.DailyCorrect);
        }

        var mastered = await _db.Set<MistakeReview>().AsNoTracking()
            .Where(m => userIds.Contains(m.UserId)
                && m.Mastered
                && m.UpdatedAt >= sinceUtc
                && m.UpdatedAt < untilUtc)
            .GroupBy(m => m.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        foreach (var r in mastered)
        {
            Add(r.UserId, r.Count * XpRules.MistakeMastered);
        }

        var games = await _db.Set<GameResult>().AsNoTracking()
            .Where(g => userIds.Contains(g.UserId)
                && g.PlayedAt >= sinceUtc
                && g.PlayedAt < untilUtc)
            .Select(g => new { g.UserId, g.Game, g.Correct, g.Won })
            .ToListAsync(ct);
        foreach (var r in games)
        {
            Add(r.UserId, r.Game is GameKinds.Duel or GameKinds.Tournament
                ? XpRules.DuelPlayed + (r.Won ? XpRules.DuelWon : 0)
                : r.Correct * XpRules.SignCorrect);
        }

        return result;
    }
}
