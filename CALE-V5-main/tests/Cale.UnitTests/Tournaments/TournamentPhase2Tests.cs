using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cale.Api.Services.Tournaments;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Domain.Gamification;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.UnitTests.Tournaments;

/// <summary>Phase 2 validation: walkover, reconnect state, load, security. Does not repeat the 13-player smoke.</summary>
public sealed class TournamentPhase2Tests : IAsyncLifetime
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
    public async Task Walkover_after_60s_awards_opponent_without_xp_and_is_idempotent()
    {
        var tournamentId = await CreateAndStartAsync(capacity: 4, takeStudents: 4);
        var match = await FirstActiveMatchAsync(tournamentId);
        Assert.NotNull(match.Player1UserId);
        Assert.NotNull(match.Player2UserId);

        var loserId = match.Player1UserId!.Value;
        var winnerId = match.Player2UserId!.Value;

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var p = await db.Set<TournamentParticipant>()
                .FirstAsync(x => x.TournamentId == tournamentId && x.UserId == loserId);
            p.IsConnected = false;
            p.DisconnectedAt = DateTime.UtcNow.AddSeconds(-TournamentService.DisconnectGraceSeconds - 5);
            await db.SaveChangesAsync();
        }

        using (var scope = _api.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<TournamentService>();
            await svc.ProcessTimeoutsAsync(CancellationToken.None);
            await svc.ProcessTimeoutsAsync(CancellationToken.None); // idempotent
        }

        using var teacher = _api.ClientFor(_api.Teacher);
        var view = await teacher.GetFromJsonAsync<MatchView>($"/api/tournaments/matches/{match.Id}", Json);
        Assert.NotNull(view);
        Assert.Equal("finished", view.Status);
        Assert.Equal(winnerId, view.WinnerUserId);
        Assert.Equal("walkover", view.FinishReason);

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var xp = await db.Set<GameResult>().CountAsync(g =>
                g.Game == GameKinds.Tournament
                && (g.UserId == winnerId || g.UserId == loserId)
                && g.SourceKey != null
                && g.SourceKey.Contains($":{match.Id}:"));
            Assert.Equal(0, xp);

            var finishedOnce = await db.Set<TournamentMatch>()
                .CountAsync(m => m.Id == match.Id && m.Status == TournamentMatchStatuses.Finished);
            Assert.Equal(1, finishedOnce);
        }
    }

    [Fact]
    public async Task Walkover_both_disconnected_picks_later_disconnect_deterministically()
    {
        var tournamentId = await CreateAndStartAsync(capacity: 4, takeStudents: 4);
        var match = await FirstActiveMatchAsync(tournamentId);
        var p1 = match.Player1UserId!.Value;
        var p2 = match.Player2UserId!.Value;

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var a = await db.Set<TournamentParticipant>().FirstAsync(x => x.TournamentId == tournamentId && x.UserId == p1);
            var b = await db.Set<TournamentParticipant>().FirstAsync(x => x.TournamentId == tournamentId && x.UserId == p2);
            a.IsConnected = false;
            b.IsConnected = false;
            // p1 left first; p2 stayed longer → p2 wins
            a.DisconnectedAt = DateTime.UtcNow.AddSeconds(-120);
            b.DisconnectedAt = DateTime.UtcNow.AddSeconds(-70);
            await db.SaveChangesAsync();
        }

        using (var scope = _api.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TournamentService>()
                .ProcessTimeoutsAsync(CancellationToken.None);
        }

        using var teacher = _api.ClientFor(_api.Teacher);
        var view = await teacher.GetFromJsonAsync<MatchView>($"/api/tournaments/matches/{match.Id}", Json);
        Assert.NotNull(view);
        Assert.Equal("walkover", view.FinishReason);
        Assert.Equal(p2, view.WinnerUserId);
    }

    [Fact]
    public async Task Reconnect_restores_persisted_match_state_from_api()
    {
        var tournamentId = await CreateAndStartAsync(capacity: 4, takeStudents: 4);
        var match = await FirstActiveMatchAsync(tournamentId);
        var p1 = match.Player1UserId!.Value;
        var student = _api.Students.First(s => s.Id == p1);

        using (var scope = _api.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<TournamentService>();
            await svc.SetConnectionAsync(tournamentId, p1, "conn-a", connected: true, CancellationToken.None);
            await svc.SetConnectionAsync(tournamentId, p1, "conn-a", connected: false, CancellationToken.None);
            await svc.SetConnectionAsync(tournamentId, p1, "conn-b", connected: true, CancellationToken.None);
        }

        using var client = _api.ClientFor(student);
        var before = await client.GetFromJsonAsync<MatchView>($"/api/tournaments/matches/{match.Id}", Json);
        Assert.NotNull(before);
        Assert.Equal("active", before.Status);
        Assert.NotNull(before.CurrentMatchQuestionId);

        // Answer one question then "reconnect" by fetching again (DB is source of truth)
        var correct = await CorrectOptionAsync(before.CurrentMatchQuestionId!.Value);
        await client.PostAsJsonAsync(
            $"/api/tournaments/matches/{match.Id}/answer",
            new { matchQuestionId = before.CurrentMatchQuestionId, optionId = correct });

        var after = await client.GetFromJsonAsync<MatchView>($"/api/tournaments/matches/{match.Id}", Json);
        Assert.NotNull(after);
        Assert.True(after.MyAnswers.Count >= 1);
        Assert.Equal(before.MatchId, after.MatchId);
    }

    [Fact]
    public async Task SignalR_hub_accepts_authenticated_JoinTournament()
    {
        var tournamentId = await CreateAndStartAsync(capacity: 4, takeStudents: 4);
        var token = _api.TokenFor(_api.Students[0]);
        var hubUrl = new Uri(_api.Server.BaseAddress!, $"/hubs/tournaments?access_token={Uri.EscapeDataString(token)}");

        await using var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _api.Server.CreateHandler();
            })
            .Build();

        var lobbyUpdates = 0;
        var reconnects = 0;
        connection.On<object>("LobbyUpdated", _ => Interlocked.Increment(ref lobbyUpdates));
        connection.On<object>("ParticipantReconnected", _ => Interlocked.Increment(ref reconnects));

        await connection.StartAsync();
        await connection.InvokeAsync("JoinTournament", tournamentId);
        await Task.Delay(300);
        Assert.Equal(HubConnectionState.Connected, connection.State);

        // Presence must not flood the group with a full LobbyUpdated (anti-storm).
        Assert.Equal(0, lobbyUpdates);
        Assert.True(reconnects >= 1);

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var p = await db.Set<TournamentParticipant>()
                .FirstAsync(x => x.TournamentId == tournamentId && x.UserId == _api.Students[0].Id);
            Assert.True(p.IsConnected);
        }

        await connection.InvokeAsync("LeaveTournament", tournamentId);
        await connection.StopAsync();
    }

    [Fact]
    public async Task SetConnection_does_not_require_LobbyUpdated_broadcast()
    {
        var tournamentId = await CreateAndStartAsync(capacity: 4, takeStudents: 4);
        var student = _api.Students[0];
        var token = _api.TokenFor(student);
        var hubUrl = new Uri(_api.Server.BaseAddress!, $"/hubs/tournaments?access_token={Uri.EscapeDataString(token)}");

        await using var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _api.Server.CreateHandler();
            })
            .Build();

        var lobbyUpdates = 0;
        var disconnects = 0;
        var reconnects = 0;
        connection.On<object>("LobbyUpdated", _ => Interlocked.Increment(ref lobbyUpdates));
        connection.On<object>("ParticipantDisconnected", _ => Interlocked.Increment(ref disconnects));
        connection.On<object>("ParticipantReconnected", _ => Interlocked.Increment(ref reconnects));

        await connection.StartAsync();
        await connection.InvokeAsync("JoinTournament", tournamentId);
        await Task.Delay(150);

        using (var scope = _api.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<TournamentService>();
            await svc.SetConnectionAsync(tournamentId, student.Id, "conn-x", connected: false, CancellationToken.None);
            await svc.SetConnectionAsync(tournamentId, student.Id, "conn-y", connected: true, CancellationToken.None);
        }

        await Task.Delay(300);
        Assert.Equal(0, lobbyUpdates);
        Assert.True(disconnects >= 1);
        Assert.True(reconnects >= 1);

        await connection.StopAsync();
    }

    [Fact]
    public async Task SignalR_teacher_spectates_match_without_player_slot_or_rewards()
    {
        var tournamentId = await CreateAndStartAsync(capacity: 4, takeStudents: 4);
        var match = await FirstActiveMatchAsync(tournamentId);
        Assert.NotNull(match.Player1UserId);
        Assert.NotNull(match.Player2UserId);
        Assert.DoesNotContain(
            _api.Teacher.Id,
            new[] { match.Player1UserId!.Value, match.Player2UserId!.Value });

        var teacherToken = _api.TokenFor(_api.Teacher);
        var hubUrl = new Uri(
            _api.Server.BaseAddress!,
            $"/hubs/tournaments?access_token={Uri.EscapeDataString(teacherToken)}");

        await using var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _api.Server.CreateHandler();
            })
            .Build();

        var matchUpdated = 0;
        var matchFinished = 0;
        connection.On<object>("MatchUpdated", _ => Interlocked.Increment(ref matchUpdated));
        connection.On<object>("MatchFinished", _ => Interlocked.Increment(ref matchFinished));

        await connection.StartAsync();
        await connection.InvokeAsync("JoinTournament", tournamentId);
        await Task.Delay(150);
        Assert.Equal(HubConnectionState.Connected, connection.State);

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            Assert.False(await db.Set<TournamentParticipant>()
                .AnyAsync(p => p.TournamentId == tournamentId && p.UserId == _api.Teacher.Id));
        }

        using (var teacher = _api.ClientFor(_api.Teacher))
        {
            var view = await teacher.GetFromJsonAsync<MatchViewDto>(
                $"/api/tournaments/matches/{match.Id}",
                Json);
            Assert.NotNull(view);
            Assert.False(view.AmIPlayer);
            Assert.Equal(HttpStatusCode.Forbidden, (await teacher.PostAsJsonAsync(
                $"/api/tournaments/matches/{match.Id}/answer",
                new { matchQuestionId = view.CurrentMatchQuestionId, optionId = 1 })).StatusCode);
        }

        await PlayMatchAsync(match.Id);

        // Hub group membership should deliver at least one live event while spectating.
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && matchUpdated == 0 && matchFinished == 0)
        {
            await Task.Delay(50);
        }

        Assert.True(
            matchUpdated + matchFinished >= 1,
            $"teacher spectator received no hub events (updated={matchUpdated}, finished={matchFinished})");

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            Assert.Equal(
                0,
                await db.Set<GameResult>()
                    .CountAsync(g => g.UserId == _api.Teacher.Id && g.Game == GameKinds.Tournament));
            Assert.Equal(
                0,
                await db.Set<TournamentTrophy>()
                    .CountAsync(t => t.UserId == _api.Teacher.Id));
            Assert.Equal(
                0,
                await db.Set<UserAchievement>()
                    .CountAsync(a => a.UserId == _api.Teacher.Id && a.Code.StartsWith("campeon_torneo")));
        }

        await connection.InvokeAsync("LeaveTournament", tournamentId);
        await connection.StopAsync();
    }

    [Fact]
    public async Task Load_sixteen_players_eight_concurrent_first_round_matches()
    {
        var sw = Stopwatch.StartNew();
        var errors = 0;
        var tournamentId = await CreateAndStartAsync(capacity: 16, takeStudents: 16);

        using var teacher = _api.ClientFor(_api.Teacher);
        var bracket = await teacher.GetFromJsonAsync<BracketDto>($"/api/tournaments/{tournamentId}/bracket", Json);
        Assert.NotNull(bracket);
        var active = bracket.Matches.Where(m => m.Status == "active").ToList();
        Assert.Equal(8, active.Count);

        // Play all 8 first-round matches concurrently
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

        bracket = await teacher.GetFromJsonAsync<BracketDto>($"/api/tournaments/{tournamentId}/bracket", Json);
        Assert.NotNull(bracket);
        Assert.Equal(0, errors);
        Assert.All(active, m =>
        {
            var updated = bracket.Matches.First(x => x.Id == m.Id);
            Assert.Equal("finished", updated.Status);
            Assert.NotNull(updated.WinnerUserId);
        });

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var keys = await db.Set<GameResult>()
                .Where(g => g.Game == GameKinds.Tournament && g.SourceKey != null)
                .Select(g => g.SourceKey!)
                .ToListAsync();
            Assert.Equal(keys.Count, keys.Distinct().Count());
        }

        // Record real timing for the informe (not invented)
        var reportPath = Path.Combine(Path.GetTempPath(), "cale-tournament-phase2-load.txt");
        await File.WriteAllTextAsync(
            reportPath,
            $"elapsedMs={sw.ElapsedMilliseconds};activeFirstRound=8;httpErrors={errors};finishedFirstRound=8");
        Assert.True(sw.Elapsed < TimeSpan.FromMinutes(3), $"Load exceeded 3m: {sw.Elapsed}");
        Assert.True(sw.ElapsedMilliseconds > 0);
    }

    [Fact]
    public async Task Security_blocks_cross_school_and_non_player_answers()
    {
        var tournamentId = await CreateAndStartAsync(capacity: 4, takeStudents: 4);
        var match = await FirstActiveMatchAsync(tournamentId);

        using var outsider = _api.ClientFor(_api.OtherSchoolStudent);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/tournaments/{tournamentId}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await outsider.PostAsJsonAsync(
                $"/api/tournaments/matches/{match.Id}/answer",
                new { matchQuestionId = 1, optionId = 1 })).StatusCode);

        var spectator = _api.Students.First(s =>
            s.Id != match.Player1UserId && s.Id != match.Player2UserId);
        using var specClient = _api.ClientFor(spectator);
        // Same school can view match (EnsureCanView), but cannot answer
        Assert.Equal(HttpStatusCode.OK, (await specClient.GetAsync($"/api/tournaments/matches/{match.Id}")).StatusCode);

        var matchView = await specClient.GetFromJsonAsync<MatchView>($"/api/tournaments/matches/{match.Id}", Json);
        Assert.NotNull(matchView);
        if (matchView.CurrentMatchQuestionId is int mq)
        {
            var ans = await specClient.PostAsJsonAsync(
                $"/api/tournaments/matches/{match.Id}/answer",
                new { matchQuestionId = mq, optionId = 1 });
            Assert.Equal(HttpStatusCode.Forbidden, ans.StatusCode);
        }

        using var anon = _api.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/tournaments")).StatusCode);
    }

    [Fact]
    public async Task MatchAssigned_notifies_winners_after_round_advances()
    {
        var tournamentId = await CreateAndStartAsync(capacity: 4, takeStudents: 4);
        using var teacher = _api.ClientFor(_api.Teacher);
        var bracket = await teacher.GetFromJsonAsync<BracketDto>($"/api/tournaments/{tournamentId}/bracket", Json);
        Assert.NotNull(bracket);
        var firstRound = bracket.Matches.Where(m => m.Status == "active").ToList();
        Assert.Equal(2, firstRound.Count);

        var watcherId = firstRound[0].Player1UserId!.Value;
        var token = _api.TokenFor(_api.Students.First(s => s.Id == watcherId));
        var hubUrl = new Uri(_api.Server.BaseAddress!, $"/hubs/tournaments?access_token={Uri.EscapeDataString(token)}");

        await using var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => _api.Server.CreateHandler();
            })
            .Build();

        var assignedMatchIds = new ConcurrentBag<int>();
        connection.On<System.Text.Json.JsonElement>("MatchAssigned", payload =>
        {
            if (payload.TryGetProperty("matchId", out var idProp) && idProp.TryGetInt32(out var mid))
            {
                assignedMatchIds.Add(mid);
            }
        });

        await connection.StartAsync();
        await connection.InvokeAsync("JoinTournament", tournamentId);

        await Task.WhenAll(firstRound.Select(m => PlayMatchAsync(m.Id)));

        // Finals activation + NotifyMatchAssignmentsAsync should push MatchAssigned to the winner.
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (DateTime.UtcNow < deadline && assignedMatchIds.IsEmpty)
        {
            await Task.Delay(100);
        }

        Assert.False(assignedMatchIds.IsEmpty);
        Assert.All(assignedMatchIds, id => Assert.DoesNotContain(id, firstRound.Select(m => m.Id)));

        using var winnerClient = _api.ClientFor(_api.Students.First(s => s.Id == watcherId));
        var winnerLobby = await winnerClient.GetFromJsonAsync<LobbyDto>($"/api/tournaments/{tournamentId}", Json);
        Assert.NotNull(winnerLobby);
        // Watcher was player1 of match 0 and PlayMatchAsync makes player1 win → must have finals seat.
        Assert.NotNull(winnerLobby.MyMatchId);
        Assert.Contains(winnerLobby.MyMatchId!.Value, assignedMatchIds);

        await connection.StopAsync();
    }

    private async Task<int> CreateAndStartAsync(int capacity, int takeStudents)
    {
        using var teacher = _api.ClientFor(_api.Teacher);
        var create = await teacher.PostAsJsonAsync("/api/tournaments", new
        {
            name = $"Phase2-{capacity}-{Guid.NewGuid():N}"[..28],
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
        var match = bracket.Matches.First(m => m.Status == "active");
        return match;
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
                await Task.Delay(20);
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
                    resp.EnsureSuccessStatusCode();
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
    private sealed record LobbyDto(int Id, string Status, int? MyMatchId);
    private sealed record BracketDto(int TournamentId, string Status, IReadOnlyList<BracketMatch> Matches);
    private sealed record BracketMatch(
        int Id,
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
        int? WinnerUserId,
        string? FinishReason,
        IReadOnlyList<AnswerDto> MyAnswers);
    private sealed record MatchViewDto(
        int MatchId,
        string Status,
        int? Player1UserId,
        int? Player2UserId,
        int? CurrentMatchQuestionId,
        bool AmIPlayer);
    private sealed record AnswerDto(int QuestionId, bool Correct);
}
