using Cale.Api.Services.Play;
using Cale.Modules.Assessment.Domain.Gamification;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Tournaments;

public sealed partial class TournamentService
{
    private async Task AwardMatchXpAsync(
        TournamentMatch match,
        int winnerUserId,
        int? loserUserId,
        CancellationToken ct)
    {
        var now = _clock.UtcNow;
        async Task TryAdd(int userId, bool won)
        {
            var key = $"tournament:{match.Id}:{userId}:{(won ? "win" : "play")}";
            if (await _db.Set<GameResult>().AnyAsync(g => g.SourceKey == key, ct))
            {
                return;
            }

            var correct = match.Player1UserId == userId ? match.Player1Correct : match.Player2Correct;
            var total = match.Questions.Count(q => !q.IsSuddenDeath);
            _db.Set<GameResult>().Add(new GameResult
            {
                UserId = userId,
                Game = GameKinds.Tournament,
                Score = correct,
                Correct = correct,
                Total = total,
                Won = won,
                OpponentId = userId == match.Player1UserId ? match.Player2UserId : match.Player1UserId,
                PlayedAt = now,
                SourceKey = key
            });
        }

        await TryAdd(winnerUserId, true);
        if (loserUserId is int loser)
        {
            await TryAdd(loser, false);
        }

        await _db.SaveChangesAsync(ct);
        await _play.CheckAchievementsAsync(winnerUserId, ct);
        if (loserUserId is int l)
        {
            await _play.CheckAchievementsAsync(l, ct);
        }
    }

    private async Task DeclareChampionAsync(
        Tournament tournament,
        int championUserId,
        TournamentMatch finalMatch,
        CancellationToken ct)
    {
        if (tournament.Status == TournamentStatuses.Finished && tournament.ChampionUserId == championUserId)
        {
            return;
        }

        tournament.ChampionUserId = championUserId;
        tournament.Status = TournamentStatuses.Finished;
        tournament.UpdatedAt = _clock.UtcNow;
        tournament.RowVersion++;

        var existingTrophy = await _db.Set<TournamentTrophy>()
            .AnyAsync(t => t.TournamentId == tournament.Id && t.UserId == championUserId, ct);
        if (!existingTrophy)
        {
            var result =
                $"Final {finalMatch.Player1Correct}-{finalMatch.Player2Correct} ({finalMatch.FinishReason})";
            _db.Set<TournamentTrophy>().Add(new TournamentTrophy
            {
                UserId = championUserId,
                TournamentId = tournament.Id,
                TournamentNameSnapshot = tournament.Name,
                ObtainedAt = _clock.UtcNow,
                ChampionResult = result
            });
        }

        await _db.SaveChangesAsync(ct);

        var badges = await _play.CheckAchievementsAsync(championUserId, ct);
        var name = await OrganizerNameAsync(championUserId, ct);
        // Prefer participant snapshot
        var snap = await _db.Set<TournamentParticipant>().AsNoTracking()
            .Where(p => p.TournamentId == tournament.Id && p.UserId == championUserId)
            .Select(p => p.DisplayNameSnapshot)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(snap))
        {
            name = snap;
        }

        await _broadcast.ChampionDeclared(tournament.Id, championUserId, name, ct);
        await _broadcast.TournamentFinished(tournament.Id, ct);
        if (badges.Count > 0)
        {
            await _broadcast.AchievementUnlocked(championUserId, badges, ct);
        }

        _logger.LogInformation(
            "Tournament {TournamentId} champion declared: {UserId}",
            tournament.Id,
            championUserId);
    }

    public async Task<IReadOnlyList<TournamentTrophyDto>> ListTrophiesAsync(
        int userId,
        CancellationToken ct)
    {
        return await _db.Set<TournamentTrophy>().AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.ObtainedAt)
            .Select(t => new TournamentTrophyDto(
                t.Id,
                t.TournamentId,
                t.TournamentNameSnapshot,
                t.ObtainedAt,
                t.ChampionResult))
            .ToListAsync(ct);
    }

    public async Task<CompetitiveProfileDto> GetCompetitiveProfileAsync(int userId, CancellationToken ct)
    {
        var stats = await _play.ComputeStatsAsync(userId, ct);
        var level = PlayService.BuildLevel(stats.Xp);
        var trophies = await ListTrophiesAsync(userId, ct);
        var achievements = await _play.GetAchievementsAsync(userId, ct);
        var played = await _db.Set<TournamentParticipant>().AsNoTracking()
            .CountAsync(p => p.UserId == userId, ct);
        var matchWins = await _db.Set<GameResult>().AsNoTracking()
            .CountAsync(g => g.UserId == userId && g.Game == GameKinds.Tournament && g.Won, ct);
        var matchLosses = await _db.Set<GameResult>().AsNoTracking()
            .CountAsync(g => g.UserId == userId && g.Game == GameKinds.Tournament && !g.Won, ct);
        var finals = await (
                from m in _db.Set<TournamentMatch>().AsNoTracking()
                join r in _db.Set<TournamentRound>().AsNoTracking() on m.RoundId equals r.Id
                where (m.Player1UserId == userId || m.Player2UserId == userId)
                    && r.Name == "Gran final"
                select m.Id).CountAsync(ct);

        // General rank: school scope, 2 years — lightweight call
        int? generalRank = null;
        try
        {
            var ranking = await _play.GetRankingAsync(userId, "school", null, ct);
            generalRank = ranking.MyPosition;
        }
        catch
        {
            // ranking optional for profile
        }

        return new CompetitiveProfileDto(
            level,
            generalRank,
            trophies.Count,
            achievements.Badges.Count(b => b.Earned),
            trophies.Count,
            played,
            matchWins,
            matchLosses,
            finals,
            trophies,
            achievements.Badges);
    }
}
