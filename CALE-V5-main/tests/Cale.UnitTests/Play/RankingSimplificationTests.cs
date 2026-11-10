using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cale.BuildingBlocks.Domain.Security;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Domain.Gamification;
using Cale.Modules.Classroom.Domain;
using Cale.Modules.Identity.Domain;
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
    private User _noSchoolNoGroup = null!;
    private User _noSchoolWithGroups = null!;
    private User _schoolNoGroup = null!;

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
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var now = DateTime.UtcNow;
        var hash = hasher.Hash(TournamentSmokeFactory.Password);

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

        // School student without any group membership (Students[3] unused by groups above).
        _schoolNoGroup = _api.Students[3];

        _noSchoolNoGroup = User.RegisterStudent(
            "Student_NoSchool",
            "student.noschool@pruebas.test",
            hash,
            now,
            schoolId: null);
        _noSchoolNoGroup.MarkEmailConfirmed();

        _noSchoolWithGroups = User.RegisterStudent(
            "Student_NoSchool_Groups",
            "student.noschool.groups@pruebas.test",
            hash,
            now,
            schoolId: null);
        _noSchoolWithGroups.MarkEmailConfirmed();

        db.AddRange(_noSchoolNoGroup, _noSchoolWithGroups);
        await db.SaveChangesAsync();

        db.Set<GroupMember>().Add(GroupMember.Join(g1.Id, _noSchoolWithGroups.Id, now));
        db.Set<GroupMember>().Add(GroupMember.Join(g2.Id, _noSchoolWithGroups.Id, now));

        db.Set<GameResult>().Add(new GameResult
        {
            UserId = _noSchoolNoGroup.Id,
            Game = GameKinds.Signs,
            Score = 40,
            Correct = 8,
            Total = 10,
            Won = true,
            PlayedAt = now,
            SourceKey = $"ranking-test:{_noSchoolNoGroup.Id}:signs"
        });
        db.Set<GameResult>().Add(new GameResult
        {
            UserId = _noSchoolWithGroups.Id,
            Game = GameKinds.Signs,
            Score = 35,
            Correct = 7,
            Total = 10,
            Won = true,
            PlayedAt = now,
            SourceKey = $"ranking-test:{_noSchoolWithGroups.Id}:signs"
        });
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

    [Fact]
    public async Task Ranking_without_school_or_group_defaults_to_global_without_school_chip()
    {
        using var student = _api.ClientFor(_noSchoolNoGroup);
        var res = await student.GetFromJsonAsync<RankingDto>("/api/student/play/ranking", Json);
        Assert.NotNull(res);
        Assert.Equal("global", res.Scope);
        Assert.Equal("Todo Luz Verde", res.ScopeLabel);
        Assert.DoesNotContain(res.Scopes, s => s.Scope == "school");
        Assert.DoesNotContain(res.Scopes, s => s.Scope == "group");
        Assert.Contains(res.Scopes, s => s.Scope == "global");
        Assert.Equal("all", res.PeriodKey);
        Assert.Equal("xp", res.Metric);
        Assert.True(res.MyXp > 0);
        Assert.All(res.Entries, e => Assert.True(e.PositionHeldSinceUtc is null || e.PositionHeldSinceUtc.Value.Year >= 2000));

        // Asking for school must not invent another school's board.
        var forcedSchool = await student.GetFromJsonAsync<RankingDto>(
            "/api/student/play/ranking?scope=school&metric=xp",
            Json);
        Assert.NotNull(forcedSchool);
        Assert.Equal("global", forcedSchool.Scope);
        Assert.DoesNotContain(forcedSchool.Scopes, s => s.Scope == "school");
    }

    [Fact]
    public async Task Ranking_without_school_but_with_groups_offers_mi_grupo_and_global()
    {
        using var student = _api.ClientFor(_noSchoolWithGroups);
        var res = await student.GetFromJsonAsync<RankingDto>("/api/student/play/ranking", Json);
        Assert.NotNull(res);
        Assert.Equal("group", res.Scope);
        Assert.Equal("Mi grupo", res.ScopeLabel);
        Assert.DoesNotContain(res.Scopes, s => s.Scope == "school");
        Assert.Contains(res.Scopes, s => s.Scope == "group" && s.Label == "Mi grupo");
        Assert.Single(res.Scopes, s => s.Scope == "group");
        Assert.Contains(res.Scopes, s => s.Scope == "global");

        var group = await student.GetFromJsonAsync<RankingDto>(
            "/api/student/play/ranking?scope=group&metric=xp",
            Json);
        Assert.NotNull(group);
        Assert.Equal("group", group.Scope);
        var ids = group.Entries.Select(e => e.UserId).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Contains(_noSchoolWithGroups.Id, ids);
        Assert.Contains(_api.Students[0].Id, ids);

        var global = await student.GetFromJsonAsync<RankingDto>(
            "/api/student/play/ranking?scope=global&metric=trophies",
            Json);
        Assert.NotNull(global);
        Assert.Equal("global", global.Scope);
        Assert.All(global.Entries, e => Assert.Null(e.PositionHeldSinceUtc));
    }

    [Fact]
    public async Task Ranking_school_student_without_groups_omits_mi_grupo()
    {
        using var student = _api.ClientFor(_schoolNoGroup);
        var res = await student.GetFromJsonAsync<RankingDto>("/api/student/play/ranking", Json);
        Assert.NotNull(res);
        Assert.Equal("school", res.Scope);
        Assert.Contains(res.Scopes, s => s.Scope == "school");
        Assert.DoesNotContain(res.Scopes, s => s.Scope == "group");
        Assert.Contains(res.Scopes, s => s.Scope == "global");
        Assert.DoesNotContain(res.Entries, e => e.UserId == _api.OtherSchoolStudent.Id);
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
        IReadOnlyList<object> Periods,
        int MyXp = 0,
        int MyTrophies = 0);

    private sealed record EntryDto(int UserId, DateTime? PositionHeldSinceUtc);
    private sealed record ScopeDto(string Scope, string Label, int? GroupId);
}
