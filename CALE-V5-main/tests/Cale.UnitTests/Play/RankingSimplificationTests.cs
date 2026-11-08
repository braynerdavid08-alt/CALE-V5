using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Domain.Gamification;
using Cale.Modules.Classroom.Domain;
using Cale.UnitTests.Tournaments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.UnitTests.Play;

/// <summary>
/// Simplified historical ranking: XP/Trofeos, school/group/global, no period UI, no sentinel holds.
/// Uses lab smoke users (@pruebas.test), not production demo accounts.
/// </summary>
public sealed class RankingSimplificationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private TournamentSmokeFactory _api = null!;

    public async Task InitializeAsync()
    {
        _api = new TournamentSmokeFactory();
        _ = _api.Server;
        await _api.SeedAsync(studentCount: 6);
        await SeedXpAndGroupsAsync();
    }

    public Task DisposeAsync()
    {
        _api.Dispose();
        return Task.CompletedTask;
    }

    private async Task SeedXpAndGroupsAsync()
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var now = DateTime.UtcNow;
        for (var i = 0; i < _api.Students.Count; i++)
        {
            var s = _api.Students[i];
            db.Set<GameResult>().Add(new GameResult
            {
                UserId = s.Id,
                Game = GameKinds.Signs,
                Score = 10 + i,
                Correct = 5 + i,
                Total = 10,
                Won = true,
                PlayedAt = now.AddDays(-i),
                SourceKey = $"ranking-test:{s.Id}:signs"
            });
        }

        // Two active groups for student[0]; union must be a single "Mi grupo" scope.
        var g1 = Group.Create("Clase A mañana", null, _api.Teacher.Id, null, now);
        var g2 = Group.Create("Clase B tarde", null, _api.Teacher.Id, null, now);
        db.Set<Group>().AddRange(g1, g2);
        await db.SaveChangesAsync();
        db.Set<GroupMember>().Add(GroupMember.Join(g1.Id, _api.Students[0].Id, now));
        db.Set<GroupMember>().Add(GroupMember.Join(g1.Id, _api.Students[1].Id, now));
        db.Set<GroupMember>().Add(GroupMember.Join(g2.Id, _api.Students[0].Id, now));
        db.Set<GroupMember>().Add(GroupMember.Join(g2.Id, _api.Students[2].Id, now));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Ranking_defaults_to_historical_xp_and_school_scope_without_period_buttons()
    {
        using var student = _api.ClientFor(_api.Students[0]);
        var res = await student.GetFromJsonAsync<RankingDto>("/api/student/play/ranking", Json);
        Assert.NotNull(res);
        Assert.Equal("school", res.Scope);
        Assert.Equal("Mi escuela", res.ScopeLabel);
        Assert.Equal("xp", res.Metric);
        Assert.Equal("all", res.PeriodKey);
        Assert.Equal(new DateOnly(2000, 1, 1), res.PeriodStart);
        Assert.Empty(res.Periods);
        Assert.DoesNotContain(res.Scopes, s => s.Label.Contains("Clase", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(res.Scopes, s => s.Scope == "school" && s.Label == "Mi escuela");
        Assert.Contains(res.Scopes, s => s.Scope == "group" && s.Label == "Mi grupo" && s.GroupId is null);
        Assert.Single(res.Scopes, s => s.Scope == "group");
        Assert.Contains(res.Scopes, s => s.Scope == "global" && s.Label == "Todo Luz Verde");
        Assert.All(res.Entries, e => Assert.True(e.PositionHeldSinceUtc is null || e.PositionHeldSinceUtc.Value.Year >= 2000));
        Assert.DoesNotContain(res.Entries, e => e.PositionHeldSinceUtc is { Year: < 2000 });
    }

    [Fact]
    public async Task Ranking_group_scope_uses_union_of_active_memberships()
    {
        using var student = _api.ClientFor(_api.Students[0]);
        var res = await student.GetFromJsonAsync<RankingDto>(
            "/api/student/play/ranking?scope=group&metric=xp",
            Json);
        Assert.NotNull(res);
        Assert.Equal("group", res.Scope);
        Assert.Equal("Mi grupo", res.ScopeLabel);
        Assert.Null(res.GroupId);
        var ids = res.Entries.Select(e => e.UserId).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Contains(_api.Students[0].Id, ids);
        Assert.Contains(_api.Students[1].Id, ids);
        Assert.Contains(_api.Students[2].Id, ids);
    }

    [Fact]
    public async Task Ranking_trophies_metric_omits_hold_dates()
    {
        using var student = _api.ClientFor(_api.Students[0]);
        var res = await student.GetFromJsonAsync<RankingDto>(
            "/api/student/play/ranking?metric=trophies&period=all&scope=school",
            Json);
        Assert.NotNull(res);
        Assert.Equal("trophies", res.Metric);
        Assert.All(res.Entries, e => Assert.Null(e.PositionHeldSinceUtc));
    }

    [Fact]
    public async Task Ranking_legacy_general_maps_to_xp_and_forces_all_period()
    {
        using var student = _api.ClientFor(_api.Students[0]);
        var res = await student.GetFromJsonAsync<RankingDto>(
            "/api/student/play/ranking?metric=general&period=30d",
            Json);
        Assert.NotNull(res);
        Assert.Equal("xp", res.Metric);
        Assert.Equal("all", res.PeriodKey);
    }

    [Fact]
    public async Task Ranking_global_does_not_leak_other_school_students_into_school_scope()
    {
        using var student = _api.ClientFor(_api.Students[0]);
        var school = await student.GetFromJsonAsync<RankingDto>(
            "/api/student/play/ranking?scope=school&metric=xp",
            Json);
        Assert.NotNull(school);
        Assert.DoesNotContain(school.Entries, e => e.UserId == _api.OtherSchoolStudent.Id);

        var global = await student.GetFromJsonAsync<RankingDto>(
            "/api/student/play/ranking?scope=global&metric=xp",
            Json);
        Assert.NotNull(global);
        Assert.Equal("global", global.Scope);
    }

    [Fact]
    public async Task Ranking_xp_hold_never_serializes_datetime_min_value()
    {
        using var student = _api.ClientFor(_api.Students[0]);
        var res = await student.GetAsync("/api/student/play/ranking?metric=xp&scope=school");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var raw = await res.Content.ReadAsStringAsync();
        Assert.DoesNotContain("0001-01-01", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("\"positionHeldSinceUtc\":\"0001", raw, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record RankingDto(
        string Scope,
        string ScopeLabel,
        int? GroupId,
        DateOnly PeriodStart,
        DateOnly PeriodEnd,
        IReadOnlyList<EntryDto> Entries,
        string Metric,
        string PeriodKey,
        IReadOnlyList<ScopeDto> Scopes,
        IReadOnlyList<object> Periods);

    private sealed record EntryDto(int UserId, DateTime? PositionHeldSinceUtc);
    private sealed record ScopeDto(string Scope, string Label, int? GroupId);
}
