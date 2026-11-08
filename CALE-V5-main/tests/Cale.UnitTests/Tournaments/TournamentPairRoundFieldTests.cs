using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Domain.Gamification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.UnitTests.Tournaments;

/// <summary>
/// Integration coverage for PairRound field sizes and teacher host/spectator rules.
/// Uses lab smoke users (@pruebas.test), not production demo accounts.
/// </summary>
public sealed class TournamentPairRoundFieldTests : IAsyncLifetime
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
    [InlineData(4, 2, 0)]
    [InlineData(5, 2, 1)]
    [InlineData(7, 3, 1)]
    [InlineData(9, 4, 1)]
    [InlineData(16, 8, 0)]
    public async Task Start_shapes_round1_with_max_pairs_and_at_most_one_bye(
        int players,
        int expectedLive,
        int expectedByes)
    {
        var tournamentId = await CreateAndStartAsync(capacity: Math.Max(players, 4), takeStudents: players);
        using var teacher = _api.ClientFor(_api.Teacher);
        var bracket = await teacher.GetFromJsonAsync<BracketDto>($"/api/tournaments/{tournamentId}/bracket", Json);
        Assert.NotNull(bracket);
        Assert.Equal(players, bracket.BracketSize);
        Assert.Equal("in_progress", bracket.Status);

        var round1 = bracket.Matches.Where(m => m.RoundNumber == 1).ToList();
        Assert.Equal(expectedLive + expectedByes, round1.Count);
        Assert.Equal(expectedByes, round1.Count(m => m.FinishReason == "bye"));
        Assert.Equal(expectedLive, round1.Count(m => m.Status == "active"));

        var seated = round1
            .SelectMany(m => new[] { m.Player1UserId, m.Player2UserId })
            .Where(id => id is int)
            .Select(id => id!.Value)
            .ToList();
        Assert.Equal(players, seated.Count);
        Assert.Equal(players, seated.Distinct().Count());

        // Persistence: re-read lobby/bracket after start.
        var lobby = await teacher.GetFromJsonAsync<LobbyDto>($"/api/tournaments/{tournamentId}", Json);
        Assert.NotNull(lobby);
        Assert.Equal(players, lobby.Participants.Count);
        Assert.Equal(0, lobby.Participants.Count(p => p.IsEliminated));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(9)]
    public async Task Odd_field_reaches_champion_with_idempotent_trophy_and_no_bye_xp(int players)
    {
        var tournamentId = await CreateAndStartAsync(capacity: Math.Max(players, 4), takeStudents: players);
        using var teacher = _api.ClientFor(_api.Teacher);

        BracketDto? bracket = null;
        for (var guard = 0; guard < 200; guard++)
        {
            bracket = await teacher.GetFromJsonAsync<BracketDto>($"/api/tournaments/{tournamentId}/bracket", Json);
            Assert.NotNull(bracket);
            if (bracket.Status == "finished")
            {
                break;
            }

            var active = bracket.Matches.Where(m => m.Status == "active").ToList();
            if (active.Count == 0)
            {
                await Task.Delay(40);
                continue;
            }

            foreach (var match in active)
            {
                await PlayMatchToCompletionAsync(match.Id);
            }
        }

        Assert.NotNull(bracket);
        Assert.Equal("finished", bracket.Status);
        Assert.NotNull(bracket.ChampionUserId);

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();

        var trophies = await db.Set<TournamentTrophy>()
            .Where(t => t.TournamentId == tournamentId)
            .ToListAsync();
        Assert.Single(trophies);
        Assert.Equal(bracket.ChampionUserId, trophies[0].UserId);

        var byeMatches = await db.Set<TournamentMatch>()
            .Where(m => m.TournamentId == tournamentId && m.FinishReason == "bye")
            .ToListAsync();
        Assert.NotEmpty(byeMatches);
        var byeOnlyKeys = byeMatches
            .Where(m => m.WinnerUserId is int)
            .Select(m => $"tournament:{m.Id}:{m.WinnerUserId}:win")
            .ToList();
        var byeXp = await db.Set<GameResult>()
            .CountAsync(g => byeOnlyKeys.Contains(g.SourceKey!));
        Assert.Equal(0, byeXp);

        var playedKeys = await db.Set<GameResult>()
            .Where(g => g.Game == GameKinds.Tournament && g.SourceKey != null)
            .Select(g => g.SourceKey!)
            .ToListAsync();
        Assert.Equal(playedKeys.Count, playedKeys.Distinct().Count());

        var eliminated = await db.Set<TournamentParticipant>()
            .CountAsync(p => p.TournamentId == tournamentId && p.IsEliminated);
        Assert.Equal(players - 1, eliminated);
        Assert.Equal(players, await db.Set<TournamentParticipant>().CountAsync(p => p.TournamentId == tournamentId));
    }

    [Fact]
    public async Task Teacher_can_spectate_match_but_cannot_answer_or_earn_student_rewards()
    {
        var tournamentId = await CreateAndStartAsync(capacity: 4, takeStudents: 4);
        using var teacher = _api.ClientFor(_api.Teacher);

        var bracket = await teacher.GetFromJsonAsync<BracketDto>($"/api/tournaments/{tournamentId}/bracket", Json);
        Assert.NotNull(bracket);
        var match = bracket.Matches.First(m => m.Status == "active");

        var viewRes = await teacher.GetAsync($"/api/tournaments/matches/{match.Id}");
        Assert.Equal(HttpStatusCode.OK, viewRes.StatusCode);
        var view = await viewRes.Content.ReadFromJsonAsync<MatchView>(Json);
        Assert.NotNull(view);
        Assert.False(view.AmIPlayer);
        Assert.NotNull(view.CurrentMatchQuestionId);

        var answer = await teacher.PostAsJsonAsync(
            $"/api/tournaments/matches/{match.Id}/answer",
            new { matchQuestionId = view.CurrentMatchQuestionId, optionId = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, answer.StatusCode);

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            Assert.False(await db.Set<TournamentParticipant>()
                .AnyAsync(p => p.TournamentId == tournamentId && p.UserId == _api.Teacher.Id));
            Assert.Equal(0, await db.Set<GameResult>()
                .CountAsync(g => g.UserId == _api.Teacher.Id && g.Game == GameKinds.Tournament));
            Assert.Equal(0, await db.Set<TournamentTrophy>()
                .CountAsync(t => t.UserId == _api.Teacher.Id && t.TournamentId == tournamentId));
        }

        // Finish tournament with students; teacher still has no tournament rewards.
        for (var guard = 0; guard < 80; guard++)
        {
            bracket = await teacher.GetFromJsonAsync<BracketDto>($"/api/tournaments/{tournamentId}/bracket", Json);
            Assert.NotNull(bracket);
            if (bracket.Status == "finished")
            {
                break;
            }

            foreach (var m in bracket.Matches.Where(x => x.Status == "active"))
            {
                await PlayMatchToCompletionAsync(m.Id);
            }
        }

        Assert.Equal("finished", bracket!.Status);
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            Assert.Equal(0, await db.Set<GameResult>()
                .CountAsync(g => g.UserId == _api.Teacher.Id && g.Game == GameKinds.Tournament));
            Assert.Equal(0, await db.Set<TournamentTrophy>()
                .CountAsync(t => t.UserId == _api.Teacher.Id));
        }
    }

    private async Task<int> CreateAndStartAsync(int capacity, int takeStudents)
    {
        using var teacher = _api.ClientFor(_api.Teacher);
        var create = await teacher.PostAsJsonAsync("/api/tournaments", new
        {
            name = $"PairRound-{takeStudents}-{Guid.NewGuid():N}"[..28],
            scheduledAt = DateTime.UtcNow.AddMinutes(20),
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

    private async Task PlayMatchToCompletionAsync(int matchId)
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

            if (view.Status != "active" || view.CurrentMatchQuestionId is not int mq)
            {
                await Task.Delay(30);
                continue;
            }

            foreach (var uid in new[] { view.Player1UserId, view.Player2UserId }.Where(x => x is int).Select(x => x!.Value))
            {
                var student = _api.Students.First(s => s.Id == uid);
                using var client = _api.ClientFor(student);
                var ans = await client.PostAsJsonAsync(
                    $"/api/tournaments/matches/{matchId}/answer",
                    new { matchQuestionId = mq, optionId = 1 });
                // Already answered / advanced is acceptable under race.
                Assert.True(
                    ans.IsSuccessStatusCode
                    || ans.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict,
                    $"unexpected answer status {ans.StatusCode}");
            }
        }

        Assert.Fail($"match {matchId} did not finish");
    }

    private sealed record IdDto(int Id);
    private sealed record LobbyDto(int Id, IReadOnlyList<ParticipantDto> Participants);
    private sealed record ParticipantDto(int UserId, bool IsEliminated);
    private sealed record BracketDto(
        int TournamentId,
        string Status,
        int BracketSize,
        IReadOnlyList<BracketMatch> Matches,
        int? ChampionUserId);
    private sealed record BracketMatch(
        int Id,
        int RoundNumber,
        int SlotIndex,
        int? Player1UserId,
        int? Player2UserId,
        string Status,
        int? WinnerUserId,
        string? FinishReason);
    private sealed record MatchView(
        int MatchId,
        string Status,
        int? Player1UserId,
        int? Player2UserId,
        int? CurrentMatchQuestionId,
        bool AmIPlayer);
}
