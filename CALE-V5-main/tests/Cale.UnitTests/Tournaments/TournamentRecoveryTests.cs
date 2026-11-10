using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Domain.Gamification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.UnitTests.Tournaments;

/// <summary>
/// Recovery of rounds left with Pending matches after a simulated crash mid-activation.
/// Lab users only (@pruebas.test) — never demo accounts.
/// </summary>
public sealed class TournamentRecoveryTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private TournamentSmokeFactory _api = null!;

    public async Task InitializeAsync()
    {
        _api = new TournamentSmokeFactory();
        _ = _api.Server;
        await _api.SeedAsync(studentCount: 16);
    }

    public Task DisposeAsync()
    {
        _api.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Interrupted_round_insert_recovers_pending_matches_to_active()
    {
        var tournamentId = await CreateAndStartAsync(4, 4);
        await SimulateCrashLeavingCurrentRoundPendingAsync(tournamentId);

        await RunRecoveryAsync();

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var t = await db.Set<Tournament>().FirstAsync(x => x.Id == tournamentId);
        Assert.Equal(TournamentStatuses.InProgress, t.Status);

        var round = await db.Set<TournamentRound>()
            .FirstAsync(r => r.TournamentId == tournamentId && r.RoundNumber == t.CurrentRoundNumber);
        var matches = await db.Set<TournamentMatch>()
            .Include(m => m.Questions)
            .Where(m => m.RoundId == round.Id)
            .ToListAsync();

        Assert.DoesNotContain(matches, m => m.Status == TournamentMatchStatuses.Pending);
        Assert.Contains(matches, m => m.Status == TournamentMatchStatuses.Active);
        Assert.All(
            matches.Where(m => m.Status == TournamentMatchStatuses.Active),
            m => Assert.True(m.Questions.Count >= 1));
    }

    [Fact]
    public async Task Recovery_retry_is_idempotent_no_duplicate_matches_or_questions()
    {
        var tournamentId = await CreateAndStartAsync(4, 4);
        await SimulateCrashLeavingCurrentRoundPendingAsync(tournamentId);

        await RunRecoveryAsync();
        await RunRecoveryAsync();
        await RunRecoveryAsync();

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var t = await db.Set<Tournament>().FirstAsync(x => x.Id == tournamentId);
        var roundId = await db.Set<TournamentRound>()
            .Where(r => r.TournamentId == tournamentId && r.RoundNumber == t.CurrentRoundNumber)
            .Select(r => r.Id)
            .FirstAsync();
        var matches = await db.Set<TournamentMatch>()
            .Include(m => m.Questions)
            .Where(m => m.RoundId == roundId)
            .ToListAsync();

        Assert.Equal(2, matches.Count);
        Assert.Equal(2, matches.Select(m => m.SlotIndex).Distinct().Count());
        Assert.DoesNotContain(matches, m => m.Status == TournamentMatchStatuses.Pending);
        foreach (var m in matches.Where(x => x.Status == TournamentMatchStatuses.Active))
        {
            Assert.Equal(m.Questions.Count, m.Questions.Select(q => q.Ordinal).Distinct().Count());
        }
    }

    [Fact]
    public async Task Concurrent_recovery_workers_activate_once_without_500()
    {
        var tournamentId = await CreateAndStartAsync(8, 8);
        await SimulateCrashLeavingCurrentRoundPendingAsync(tournamentId);

        var errors = new ConcurrentBag<Exception>();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            try
            {
                await RunRecoveryAsync();
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }));

        Assert.Empty(errors);

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var t = await db.Set<Tournament>().FirstAsync(x => x.Id == tournamentId);
        var pending = await db.Set<TournamentMatch>()
            .CountAsync(m =>
                m.TournamentId == tournamentId
                && m.Round!.RoundNumber == t.CurrentRoundNumber
                && m.Status == TournamentMatchStatuses.Pending);
        Assert.Equal(0, pending);
        var live = await db.Set<TournamentMatch>()
            .CountAsync(m =>
                m.TournamentId == tournamentId
                && m.Round!.RoundNumber == t.CurrentRoundNumber
                && m.Status == TournamentMatchStatuses.Active);
        Assert.True(live >= 1);
    }

    [Fact]
    public async Task Partial_recovery_only_touches_pending_seats()
    {
        var tournamentId = await CreateAndStartAsync(4, 4);
        int activeId;
        int pendingId;
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var t = await db.Set<Tournament>().FirstAsync(x => x.Id == tournamentId);
            var matches = await db.Set<TournamentMatch>()
                .Include(m => m.Questions)
                .Where(m => m.TournamentId == tournamentId && m.Round!.RoundNumber == t.CurrentRoundNumber)
                .OrderBy(m => m.SlotIndex)
                .ToListAsync();
            Assert.True(matches.Count >= 2);

            // Leave one Active, force one back to Pending (crash mid-activation of that seat).
            var keepActive = matches[0];
            var forcePending = matches[1];
            forcePending.Status = TournamentMatchStatuses.Pending;
            forcePending.StartedAt = null;
            forcePending.QuestionStartedAt = null;
            forcePending.CurrentQuestionIndex = 0;
            db.RemoveRange(forcePending.Questions);
            forcePending.RowVersion++;
            await db.SaveChangesAsync();

            activeId = keepActive.Id;
            pendingId = forcePending.Id;
        }

        await RunRecoveryAsync();

        // Fresh scope: recovery ran in another DbContext; tracked entities would be stale.
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var after = await db.Set<TournamentMatch>()
                .Include(m => m.Questions)
                .Where(m => m.Id == activeId || m.Id == pendingId)
                .ToListAsync();
            var stillActive = after.First(m => m.Id == activeId);
            var recovered = after.First(m => m.Id == pendingId);
            Assert.Equal(TournamentMatchStatuses.Active, stillActive.Status);
            Assert.Equal(TournamentMatchStatuses.Active, recovered.Status);
            Assert.True(stillActive.Questions.Count >= 1);
            Assert.True(recovered.Questions.Count >= 1);
        }
    }

    [Fact]
    public async Task Recovery_skips_finished_tournaments()
    {
        var tournamentId = await CreateAndStartAsync(4, 4);
        await PlayToChampionAsync(tournamentId);

        int matchId;
        int? champion;
        int trophiesBefore;
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var t = await db.Set<Tournament>().FirstAsync(x => x.Id == tournamentId);
            Assert.Equal(TournamentStatuses.Finished, t.Status);
            champion = t.ChampionUserId;
            trophiesBefore = await db.Set<TournamentTrophy>().CountAsync(x => x.TournamentId == tournamentId);

            // Corrupt a finished match to Pending — recovery must not revive the tournament.
            var match = await db.Set<TournamentMatch>()
                .Include(m => m.Questions)
                .Where(m => m.TournamentId == tournamentId)
                .OrderByDescending(m => m.Id)
                .FirstAsync();
            matchId = match.Id;
            match.Status = TournamentMatchStatuses.Pending;
            db.RemoveRange(match.Questions);
            await db.SaveChangesAsync();
        }

        await RunRecoveryAsync();

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var t = await db.Set<Tournament>().FirstAsync(x => x.Id == tournamentId);
            Assert.Equal(TournamentStatuses.Finished, t.Status);
            Assert.Equal(champion, t.ChampionUserId);
            Assert.Equal(
                trophiesBefore,
                await db.Set<TournamentTrophy>().CountAsync(x => x.TournamentId == tournamentId));
            var match = await db.Set<TournamentMatch>().FirstAsync(m => m.Id == matchId);
            Assert.Equal(TournamentMatchStatuses.Pending, match.Status);
        }
    }

    [Fact]
    public async Task Recovery_preserves_bye_bracket_and_finishes_pending_bye()
    {
        var tournamentId = await CreateAndStartAsync(4, 4);
        int byeMatchId;
        int winnerId;
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var t = await db.Set<Tournament>().FirstAsync(x => x.Id == tournamentId);
            var matches = await db.Set<TournamentMatch>()
                .Include(m => m.Questions)
                .Where(m => m.TournamentId == tournamentId && m.Round!.RoundNumber == t.CurrentRoundNumber)
                .OrderBy(m => m.SlotIndex)
                .ToListAsync();
            Assert.True(matches.Count >= 2);

            var bye = matches[0];
            Assert.NotNull(bye.Player1UserId);
            winnerId = bye.Player1UserId!.Value;
            bye.Status = TournamentMatchStatuses.Pending;
            bye.Player2UserId = null;
            bye.Player2IsBye = true;
            bye.Player1IsBye = false;
            bye.WinnerUserId = null;
            bye.FinishReason = null;
            bye.StartedAt = null;
            bye.FinishedAt = null;
            db.RemoveRange(bye.Questions);
            bye.RowVersion++;
            byeMatchId = bye.Id;

            // Leave the other seat Pending without players setup change — full crash mid-activation.
            var other = matches[1];
            other.Status = TournamentMatchStatuses.Pending;
            other.StartedAt = null;
            other.QuestionStartedAt = null;
            db.RemoveRange(other.Questions);
            other.RowVersion++;
            await db.SaveChangesAsync();
        }

        await RunRecoveryAsync();

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var bye = await db.Set<TournamentMatch>().FirstAsync(m => m.Id == byeMatchId);
            Assert.Equal(TournamentMatchStatuses.Finished, bye.Status);
            Assert.Equal(TournamentMatchFinishReasons.Bye, bye.FinishReason);
            Assert.Equal(winnerId, bye.WinnerUserId);
            Assert.True(bye.Player2IsBye);
            Assert.Null(bye.Player2UserId);

            var live = await db.Set<TournamentMatch>()
                .CountAsync(m =>
                    m.TournamentId == tournamentId
                    && m.Status == TournamentMatchStatuses.Active);
            Assert.True(live >= 1);
        }
    }

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    public async Task Recovered_field_reaches_single_champion(int players)
    {
        var tournamentId = await CreateAndStartAsync(players, players);
        // Crash after first round would have been built: finish R1 then leave R2 pending.
        await FinishAllActiveConcurrentAsync(tournamentId);
        await SimulateCrashLeavingCurrentRoundPendingAsync(tournamentId);
        await RunRecoveryAsync();

        await PlayToChampionAsync(tournamentId);

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var t = await db.Set<Tournament>().FirstAsync(x => x.Id == tournamentId);
        Assert.Equal(TournamentStatuses.Finished, t.Status);
        Assert.NotNull(t.ChampionUserId);
        Assert.Equal(1, await db.Set<TournamentTrophy>().CountAsync(x => x.TournamentId == tournamentId));

        var keys = await db.Set<GameResult>()
            .Where(g => g.Game == GameKinds.Tournament && g.SourceKey != null)
            .Select(g => g.SourceKey!)
            .ToListAsync();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public async Task Repeated_recovery_does_not_duplicate_xp_or_trophies()
    {
        var tournamentId = await CreateAndStartAsync(4, 4);
        await PlayToChampionAsync(tournamentId);

        for (var i = 0; i < 5; i++)
        {
            await RunRecoveryAsync();
        }

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        Assert.Equal(1, await db.Set<TournamentTrophy>().CountAsync(x => x.TournamentId == tournamentId));
        var keys = await db.Set<GameResult>()
            .Where(g => g.Game == GameKinds.Tournament && g.SourceKey != null)
            .Select(g => g.SourceKey!)
            .ToListAsync();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    private async Task SimulateCrashLeavingCurrentRoundPendingAsync(int tournamentId)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var t = await db.Set<Tournament>().FirstAsync(x => x.Id == tournamentId);
        Assert.Equal(TournamentStatuses.InProgress, t.Status);

        var matches = await db.Set<TournamentMatch>()
            .Include(m => m.Questions)
            .Where(m => m.TournamentId == tournamentId && m.Round!.RoundNumber == t.CurrentRoundNumber)
            .ToListAsync();
        Assert.NotEmpty(matches);

        foreach (var m in matches)
        {
            if (m.Status == TournamentMatchStatuses.Finished
                && string.Equals(m.FinishReason, TournamentMatchFinishReasons.Bye, StringComparison.OrdinalIgnoreCase))
            {
                continue; // keep BYE results for bracket integrity scenarios
            }

            m.Status = TournamentMatchStatuses.Pending;
            m.WinnerUserId = null;
            m.FinishReason = null;
            m.StartedAt = null;
            m.FinishedAt = null;
            m.QuestionStartedAt = null;
            m.CurrentQuestionIndex = 0;
            m.Player1Correct = 0;
            m.Player2Correct = 0;
            m.Player1TimeMs = 0;
            m.Player2TimeMs = 0;
            db.RemoveRange(m.Questions);
            m.RowVersion++;
        }

        await db.SaveChangesAsync();
    }

    private async Task RunRecoveryAsync()
    {
        using var scope = _api.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<Cale.Api.Services.Tournaments.TournamentService>();
        await service.RecoverStuckPendingRoundsAsync(CancellationToken.None);
    }

    private async Task FinishAllActiveConcurrentAsync(int tournamentId)
    {
        using var teacher = _api.ClientFor(_api.Teacher);
        var bracket = await teacher.GetFromJsonAsync<BracketDto>($"/api/tournaments/{tournamentId}/bracket", Json);
        Assert.NotNull(bracket);
        var active = bracket.Matches.Where(m => m.Status == "active").ToList();
        await Task.WhenAll(active.Select(m => PlayMatchAsync(m.Id)));
    }

    private async Task PlayToChampionAsync(int tournamentId)
    {
        using var teacher = _api.ClientFor(_api.Teacher);
        for (var guard = 0; guard < 120; guard++)
        {
            var bracket = await teacher.GetFromJsonAsync<BracketDto>($"/api/tournaments/{tournamentId}/bracket", Json);
            Assert.NotNull(bracket);
            if (bracket.Status == "finished")
            {
                return;
            }

            var active = bracket.Matches.Where(m => m.Status == "active").ToList();
            if (active.Count == 0)
            {
                await RunRecoveryAsync();
                await Task.Delay(20);
                continue;
            }

            await Task.WhenAll(active.Select(m => PlayMatchAsync(m.Id)));
        }

        var final = await teacher.GetFromJsonAsync<BracketDto>($"/api/tournaments/{tournamentId}/bracket", Json);
        Assert.Equal("finished", final!.Status);
    }

    private async Task<int> CreateAndStartAsync(int capacity, int takeStudents)
    {
        using var teacher = _api.ClientFor(_api.Teacher);
        var create = await teacher.PostAsJsonAsync("/api/tournaments", new
        {
            name = $"Rec-{capacity}-{Guid.NewGuid():N}"[..28],
            scheduledAt = DateTime.UtcNow.AddMinutes(30),
            capacity,
            secondsPerQuestion = 20,
            questionMode = "random"
        });
        create.EnsureSuccessStatusCode();
        var item = await create.Content.ReadFromJsonAsync<IdDto>(Json);
        Assert.NotNull(item);
        foreach (var s in _api.Students.Take(takeStudents))
        {
            using var c = _api.ClientFor(s);
            (await c.PostAsync($"/api/tournaments/{item.Id}/join", null)).EnsureSuccessStatusCode();
        }

        (await teacher.PostAsync($"/api/tournaments/{item.Id}/start", null)).EnsureSuccessStatusCode();
        return item.Id;
    }

    private async Task PlayMatchAsync(int matchId)
    {
        for (var i = 0; i < 40; i++)
        {
            using var teacher = _api.ClientFor(_api.Teacher);
            var view = await teacher.GetFromJsonAsync<MatchView>($"/api/tournaments/matches/{matchId}", Json);
            Assert.NotNull(view);
            if (view.Status == "finished")
            {
                return;
            }

            if (view.Status != "active" || view.CurrentMatchQuestionId is null)
            {
                await Task.Delay(15);
                continue;
            }

            var correct = await CorrectOptionAsync(view.CurrentMatchQuestionId.Value);
            foreach (var uid in new[] { view.Player1UserId, view.Player2UserId }.OfType<int>())
            {
                var player = _api.Students.First(s => s.Id == uid);
                using var client = _api.ClientFor(player);
                var option = uid == view.Player1UserId ? correct : correct + 999_999;
                var resp = await client.PostAsJsonAsync(
                    $"/api/tournaments/matches/{matchId}/answer",
                    new { matchQuestionId = view.CurrentMatchQuestionId, optionId = option });
                if (!resp.IsSuccessStatusCode && resp.StatusCode != HttpStatusCode.BadRequest)
                {
                    Assert.Fail($"HTTP {(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
                }
            }
        }
    }

    private async Task<int> CorrectOptionAsync(int matchQuestionId)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        return await db.Set<TournamentMatchQuestion>()
            .Where(x => x.Id == matchQuestionId)
            .Select(x => x.CorrectOptionId)
            .FirstAsync();
    }

    private sealed record IdDto(int Id);
    private sealed record BracketDto(int TournamentId, string Status, int? ChampionUserId, IReadOnlyList<BracketMatch> Matches);
    private sealed record BracketMatch(int Id, int RoundNumber, string Status, int? Player1UserId, int? Player2UserId, int? WinnerUserId);
    private sealed record MatchView(
        int MatchId,
        string Status,
        int? Player1UserId,
        int? Player2UserId,
        int? CurrentMatchQuestionId);
}
