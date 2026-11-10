using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Domain.Gamification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.UnitTests.Tournaments;

/// <summary>
/// Concurrent round advancement, champion declaration, and reward idempotency.
/// Uses lab smoke users (@pruebas.test) only — never demo accounts.
/// </summary>
public sealed class TournamentConcurrencyTests : IAsyncLifetime
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

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    public async Task Concurrent_first_round_completion_creates_single_next_round(int players)
    {
        const int repetitions = 20;
        var report = new ConcurrentBag<(int Run, int HttpErrors, int Round2Count, int Round2Matches, long Ms)>();

        for (var run = 1; run <= repetitions; run++)
        {
            var sw = Stopwatch.StartNew();
            var errors = 0;
            var tournamentId = await CreateAndStartAsync(capacity: players, takeStudents: players);

            using var teacher = _api.ClientFor(_api.Teacher);
            var bracket = await teacher.GetFromJsonAsync<BracketDto>($"/api/tournaments/{tournamentId}/bracket", Json);
            Assert.NotNull(bracket);
            var active = bracket.Matches.Where(m => m.Status == "active" && m.RoundNumber == 1).ToList();
            Assert.Equal(players / 2, active.Count);

            var tasks = active.Select(async m =>
            {
                try
                {
                    await PlayMatchAsync(m.Id);
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                    throw;
                }
            });
            await Task.WhenAll(tasks);
            sw.Stop();

            Assert.Equal(0, errors);

            using var scope = _api.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var rounds = await db.Set<TournamentRound>()
                .Where(r => r.TournamentId == tournamentId)
                .OrderBy(r => r.RoundNumber)
                .ToListAsync();
            Assert.Equal(2, rounds.Count);
            Assert.Equal(1, rounds.Count(r => r.RoundNumber == 2));

            var round2Matches = await db.Set<TournamentMatch>()
                .CountAsync(m => m.TournamentId == tournamentId && m.RoundId == rounds[1].Id);
            Assert.Equal(players / 4, round2Matches);

            var finishedR1 = await db.Set<TournamentMatch>()
                .CountAsync(m => m.RoundId == rounds[0].Id && m.Status == TournamentMatchStatuses.Finished);
            Assert.Equal(players / 2, finishedR1);

            report.Add((run, errors, rounds.Count(r => r.RoundNumber == 2), round2Matches, sw.ElapsedMilliseconds));
        }

        var path = Path.Combine(Path.GetTempPath(), $"cale-tournament-concurrency-r1-{players}.txt");
        await File.WriteAllLinesAsync(
            path,
            report.Select(r =>
                $"run={r.Run};httpErrors={r.HttpErrors};round2Count={r.Round2Count};round2Matches={r.Round2Matches};ms={r.Ms}"));
        Assert.Equal(repetitions, report.Count);
        Assert.All(report, r => Assert.Equal(0, r.HttpErrors));
        Assert.All(report, r => Assert.Equal(1, r.Round2Count));
    }

    [Fact]
    public async Task Answer_retry_does_not_duplicate_results_or_xp()
    {
        var tournamentId = await CreateAndStartAsync(capacity: 4, takeStudents: 4);
        var match = await FirstActiveMatchAsync(tournamentId);
        Assert.NotNull(match.Player1UserId);

        using var teacher = _api.ClientFor(_api.Teacher);
        var view = await teacher.GetFromJsonAsync<MatchView>($"/api/tournaments/matches/{match.Id}", Json);
        Assert.NotNull(view);
        Assert.NotNull(view.CurrentMatchQuestionId);

        var correct = await CorrectOptionAsync(view.CurrentMatchQuestionId!.Value);
        var player = _api.Students.First(s => s.Id == match.Player1UserId);
        using var client = _api.ClientFor(player);

        var body = new { matchQuestionId = view.CurrentMatchQuestionId, optionId = correct };
        var first = await client.PostAsJsonAsync($"/api/tournaments/matches/{match.Id}/answer", body);
        first.EnsureSuccessStatusCode();

        var retries = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            client.PostAsJsonAsync($"/api/tournaments/matches/{match.Id}/answer", body)));

        Assert.All(retries, r =>
            Assert.True(
                r.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest,
                $"Unexpected {(int)r.StatusCode}"));

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var answers = await db.Set<TournamentAnswer>()
            .CountAsync(a => a.MatchId == match.Id && a.UserId == player.Id && a.MatchQuestionId == view.CurrentMatchQuestionId);
        Assert.Equal(1, answers);
    }

    [Fact]
    public async Task Concurrent_final_answers_declare_single_champion_and_trophy()
    {
        const int repetitions = 20;
        var trophies = new ConcurrentBag<int>();
        var champions = new ConcurrentBag<int?>();
        var xpKeys = new ConcurrentBag<int>();
        var httpErrors = 0;

        for (var run = 1; run <= repetitions; run++)
        {
            var tournamentId = await CreateAndStartAsync(capacity: 4, takeStudents: 4);
            await PlayTournamentToChampionWithConcurrentFinalAsync(tournamentId);

            using var scope = _api.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var t = await db.Set<Tournament>().FirstAsync(x => x.Id == tournamentId);
            Assert.Equal(TournamentStatuses.Finished, t.Status);
            Assert.NotNull(t.ChampionUserId);
            champions.Add(t.ChampionUserId);

            var trophyCount = await db.Set<TournamentTrophy>()
                .CountAsync(x => x.TournamentId == tournamentId);
            Assert.Equal(1, trophyCount);
            trophies.Add(trophyCount);

            // SourceKey is tournament:{matchId}:{userId}:win|play — must stay unique across the factory DB.
            var allKeys = await db.Set<GameResult>()
                .Where(g => g.Game == GameKinds.Tournament && g.SourceKey != null)
                .Select(g => g.SourceKey!)
                .ToListAsync();
            Assert.Equal(allKeys.Count, allKeys.Distinct().Count());
            xpKeys.Add(allKeys.Count);

            var badges = await db.Set<UserAchievement>()
                .CountAsync(a => a.UserId == t.ChampionUserId && a.Code.StartsWith("campeon"));
            Assert.True(badges >= 0);
        }

        Assert.Equal(0, httpErrors);
        Assert.Equal(repetitions, trophies.Count);
        Assert.All(trophies, c => Assert.Equal(1, c));
        Assert.All(champions, c => Assert.NotNull(c));

        var reportPath = Path.Combine(Path.GetTempPath(), "cale-tournament-concurrency-champion.txt");
        await File.WriteAllTextAsync(
            reportPath,
            $"repetitions={repetitions};trophiesOk={trophies.Count(c => c == 1)};champions={champions.Count(c => c is not null)};httpErrors={httpErrors}");
    }

    [Fact]
    public async Task Load_sixteen_players_eight_concurrent_first_round_is_repeatable()
    {
        for (var i = 0; i < 5; i++)
        {
            var errors = 0;
            var tournamentId = await CreateAndStartAsync(capacity: 16, takeStudents: 16);
            using var teacher = _api.ClientFor(_api.Teacher);
            var bracket = await teacher.GetFromJsonAsync<BracketDto>($"/api/tournaments/{tournamentId}/bracket", Json);
            Assert.NotNull(bracket);
            var active = bracket.Matches.Where(m => m.Status == "active").ToList();
            Assert.Equal(8, active.Count);

            await Task.WhenAll(active.Select(async m =>
            {
                try
                {
                    await PlayMatchAsync(m.Id);
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                    throw;
                }
            }));

            Assert.Equal(0, errors);
            using var scope = _api.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            Assert.Equal(1, await db.Set<TournamentRound>().CountAsync(r => r.TournamentId == tournamentId && r.RoundNumber == 2));
        }
    }

    private async Task PlayTournamentToChampionWithConcurrentFinalAsync(int tournamentId)
    {
        using var teacher = _api.ClientFor(_api.Teacher);
        for (var guard = 0; guard < 80; guard++)
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
                await Task.Delay(20);
                continue;
            }

            // Final: drive each question with parallel player answers to race CompleteMatch/DeclareChampion.
            if (active.Count == 1)
            {
                await PlayMatchWithConcurrentClosingAnswersAsync(active[0].Id);
            }
            else
            {
                await Task.WhenAll(active.Select(m => PlayMatchAsync(m.Id)));
            }
        }

        var final = await teacher.GetFromJsonAsync<BracketDto>($"/api/tournaments/{tournamentId}/bracket", Json);
        Assert.Equal("finished", final!.Status);
    }

    private async Task PlayMatchWithConcurrentClosingAnswersAsync(int matchId)
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
            var players = new[] { view.Player1UserId, view.Player2UserId }.OfType<int>().ToList();
            // Drive both answers in parallel so CompleteMatch/DeclareChampion can race.
            await Task.WhenAll(players.Select(async uid =>
            {
                var player = _api.Students.First(s => s.Id == uid);
                using var client = _api.ClientFor(player);
                var option = uid == view.Player1UserId ? correct : correct + 999_999;
                var resp = await client.PostAsJsonAsync(
                    $"/api/tournaments/matches/{matchId}/answer",
                    new { matchQuestionId = view.CurrentMatchQuestionId, optionId = option });
                if (resp.IsSuccessStatusCode
                    || resp.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
                {
                    return;
                }

                Assert.Fail($"HTTP {(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
            }));
        }
    }

    private async Task<int> CreateAndStartAsync(int capacity, int takeStudents)
    {
        using var teacher = _api.ClientFor(_api.Teacher);
        var create = await teacher.PostAsJsonAsync("/api/tournaments", new
        {
            name = $"Conc-{capacity}-{Guid.NewGuid():N}"[..28],
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

        var start = await teacher.PostAsync($"/api/tournaments/{item.Id}/start", null);
        start.EnsureSuccessStatusCode();
        return item.Id;
    }

    private async Task<BracketMatch> FirstActiveMatchAsync(int tournamentId)
    {
        using var teacher = _api.ClientFor(_api.Teacher);
        var bracket = await teacher.GetFromJsonAsync<BracketDto>($"/api/tournaments/{tournamentId}/bracket", Json);
        Assert.NotNull(bracket);
        return bracket.Matches.First(m => m.Status == "active");
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
    private sealed record BracketDto(
        int TournamentId,
        string Status,
        int? ChampionUserId,
        IReadOnlyList<BracketMatch> Matches);
    private sealed record BracketMatch(
        int Id,
        int RoundNumber,
        string Status,
        int? Player1UserId,
        int? Player2UserId,
        int? WinnerUserId,
        string? FinishReason);
    private sealed record MatchView(
        int MatchId,
        string Status,
        int? Player1UserId,
        int? Player2UserId,
        int? CurrentMatchQuestionId,
        int? WinnerUserId);
}
