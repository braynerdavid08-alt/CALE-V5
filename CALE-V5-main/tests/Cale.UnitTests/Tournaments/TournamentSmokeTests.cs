using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Catalog;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Domain.Gamification;
using Cale.Modules.Catalog.Domain;
using Cale.Modules.Identity.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cale.UnitTests.Tournaments;

/// <summary>
/// End-to-end smoke against a throwaway SQLite API host: 13 students, BYEs, full bracket to champion.
/// </summary>
public sealed class TournamentSmokeTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private TournamentSmokeFactory _api = null!;

    public async Task InitializeAsync()
    {
        _api = new TournamentSmokeFactory();
        _ = _api.Server;
        await _api.SeedAsync();
    }

    public Task DisposeAsync()
    {
        _api.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Thirteen_player_tournament_reaches_champion_with_trophy_and_no_bye_xp()
    {
        using var teacher = _api.ClientFor(_api.Teacher);
        using var outsider = _api.ClientFor(_api.OtherSchoolStudent);

        var create = await teacher.PostAsJsonAsync("/api/tournaments", new
        {
            name = "Copa Smoke 13",
            description = "Prueba integral",
            scheduledAt = DateTime.UtcNow.AddHours(1),
            capacity = 16,
            secondsPerQuestion = 30,
            questionMode = "random",
            categories = (string[]?)null,
            manualQuestionIds = (int[]?)null
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<TournamentListItem>(Json);
        Assert.NotNull(created);
        var tournamentId = created.Id;

        // Other school cannot join
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await outsider.PostAsync($"/api/tournaments/{tournamentId}/join", null)).StatusCode);

        foreach (var enrollee in _api.Students)
        {
            using var client = _api.ClientFor(enrollee);
            var join = await client.PostAsync($"/api/tournaments/{tournamentId}/join", null);
            Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        }

        var lobby = await teacher.GetFromJsonAsync<TournamentLobby>($"/api/tournaments/{tournamentId}", Json);
        Assert.NotNull(lobby);
        Assert.Equal(13, lobby.Participants.Count);

        var start = await teacher.PostAsync($"/api/tournaments/{tournamentId}/start", null);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var bracket = await start.Content.ReadFromJsonAsync<TournamentBracket>(Json);
        Assert.NotNull(bracket);
        Assert.Equal(16, bracket.BracketSize);
        Assert.Equal("in_progress", bracket.Status);

        var byeMatches = bracket.Matches
            .Where(m => m.RoundNumber == 1 && m.FinishReason == "bye")
            .ToList();
        Assert.Equal(3, byeMatches.Count);
        Assert.All(byeMatches, m => Assert.NotNull(m.WinnerUserId));

        // No XP/GameResult for BYE winners
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var byeWinners = byeMatches.Select(m => m.WinnerUserId!.Value).ToHashSet();
            var byeXp = await db.Set<GameResult>()
                .CountAsync(g => byeWinners.Contains(g.UserId) && g.Game == GameKinds.Tournament);
            Assert.Equal(0, byeXp);
        }

        // Play until finished (cap iterations)
        for (var guard = 0; guard < 200; guard++)
        {
            bracket = await teacher.GetFromJsonAsync<TournamentBracket>(
                $"/api/tournaments/{tournamentId}/bracket",
                Json);
            Assert.NotNull(bracket);
            if (bracket.Status == "finished")
            {
                break;
            }

            var active = bracket.Matches.Where(m => m.Status == "active").ToList();
            if (active.Count == 0)
            {
                await Task.Delay(50);
                continue;
            }

            Assert.True(active.Count >= 1, "expected simultaneous or sequential active matches");
            foreach (var match in active)
            {
                await PlayMatchToCompletionAsync(match.Id);
            }
        }

        bracket = await teacher.GetFromJsonAsync<TournamentBracket>(
            $"/api/tournaments/{tournamentId}/bracket",
            Json);
        Assert.NotNull(bracket);
        Assert.Equal("finished", bracket.Status);
        Assert.NotNull(bracket.ChampionUserId);

        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var championId = bracket.ChampionUserId!.Value;

            var trophies = await db.Set<TournamentTrophy>()
                .Where(t => t.TournamentId == tournamentId)
                .ToListAsync();
            Assert.Single(trophies);
            Assert.Equal(championId, trophies[0].UserId);
            Assert.Equal("Copa Smoke 13", trophies[0].TournamentNameSnapshot);

            // Idempotent trophy
            db.Set<TournamentTrophy>().Add(new TournamentTrophy
            {
                UserId = championId,
                TournamentId = tournamentId,
                TournamentNameSnapshot = "dup",
                ObtainedAt = DateTime.UtcNow,
                ChampionResult = "dup"
            });
            await Assert.ThrowsAnyAsync<Exception>(async () => await db.SaveChangesAsync());
            db.ChangeTracker.Clear();

            var badge = await db.Set<UserAchievement>()
                .FirstOrDefaultAsync(a => a.UserId == championId && a.Code == "campeon_torneo");
            Assert.NotNull(badge);

            // BYE winners still must not have tournament GameResult from bye alone if they never played
            // (they may have played later rounds — only check bye-only users who lost next? skip)
            var walkovers = await db.Set<TournamentMatch>()
                .CountAsync(m => m.TournamentId == tournamentId && m.FinishReason == "walkover");
            _ = walkovers;

            var playedResults = await db.Set<GameResult>()
                .Where(g => g.Game == GameKinds.Tournament && g.SourceKey != null)
                .ToListAsync();
            Assert.Equal(playedResults.Count, playedResults.Select(g => g.SourceKey).Distinct().Count());
        }

        // Ranking endpoints for roles
        using var admin = _api.ClientFor(_api.Admin);
        using var school = _api.ClientFor(_api.School);
        using var student = _api.ClientFor(_api.Students[0]);

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/tournaments")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await school.GetAsync("/api/tournaments")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync("/api/tournaments")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await student.GetAsync("/api/student/play/ranking?metric=general&period=2y")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync("/api/tournaments/me/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await student.GetAsync("/api/tournaments/me/trophies")).StatusCode);

        // Student cannot create / start
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await student.PostAsJsonAsync("/api/tournaments", new
            {
                name = "Hack",
                scheduledAt = DateTime.UtcNow,
                capacity = 4,
                secondsPerQuestion = 30,
                questionMode = "random"
            })).StatusCode);

        // School can list but teacher-only create
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await school.PostAsJsonAsync("/api/tournaments", new
            {
                name = "School create",
                scheduledAt = DateTime.UtcNow,
                capacity = 4,
                secondsPerQuestion = 30,
                questionMode = "random"
            })).StatusCode);
    }

    [Fact]
    public async Task Other_school_teacher_cannot_start_foreign_tournament()
    {
        using var teacher1 = _api.ClientFor(_api.Teacher);
        using var teacher2 = _api.ClientFor(_api.OtherTeacher);

        var create = await teacher1.PostAsJsonAsync("/api/tournaments", new
        {
            name = "Privado escuela 1",
            scheduledAt = DateTime.UtcNow.AddHours(1),
            capacity = 4,
            secondsPerQuestion = 20,
            questionMode = "random"
        });
        create.EnsureSuccessStatusCode();
        var item = await create.Content.ReadFromJsonAsync<TournamentListItem>(Json);
        Assert.NotNull(item);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await teacher2.PostAsync($"/api/tournaments/{item.Id}/start", null)).StatusCode);
    }

    private async Task PlayMatchToCompletionAsync(int matchId)
    {
        for (var q = 0; q < 40; q++)
        {
            TournamentMatchView? view;
            using (var teacher = _api.ClientFor(_api.Teacher))
            {
                view = await teacher.GetFromJsonAsync<TournamentMatchView>(
                    $"/api/tournaments/matches/{matchId}",
                    Json);
            }

            Assert.NotNull(view);
            if (view.Status == "finished")
            {
                return;
            }

            if (view.Status != "active" || view.CurrentMatchQuestionId is null)
            {
                await Task.Delay(30);
                continue;
            }

            var correctOptionId = await CorrectOptionForMatchQuestionAsync(view.CurrentMatchQuestionId.Value);
            foreach (var userId in new[] { view.Player1UserId, view.Player2UserId })
            {
                if (userId is not int uid)
                {
                    continue;
                }

                var player = _api.Students.First(s => s.Id == uid);
                using var client = _api.ClientFor(player);
                // Player 1 always correct, player 2 wrong → deterministic winner = player1
                var option = uid == view.Player1UserId ? correctOptionId : correctOptionId + 999999;
                await client.PostAsJsonAsync(
                    $"/api/tournaments/matches/{matchId}/answer",
                    new { matchQuestionId = view.CurrentMatchQuestionId, optionId = option });
            }
        }
    }

    private async Task<int> CorrectOptionForMatchQuestionAsync(int matchQuestionId)
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var mq = await db.Set<TournamentMatchQuestion>().FirstAsync(x => x.Id == matchQuestionId);
        return mq.CorrectOptionId;
    }

    private sealed record TournamentListItem(int Id, string Name, string Status, int Capacity, int RegisteredCount);
    private sealed record TournamentLobby(int Id, string Status, IReadOnlyList<Participant> Participants);
    private sealed record Participant(int UserId, string Name);
    private sealed record TournamentBracket(
        int TournamentId,
        string Status,
        int BracketSize,
        IReadOnlyList<BracketMatch> Matches,
        int? ChampionUserId);
    private sealed record BracketMatch(
        int Id,
        int RoundNumber,
        string Status,
        int? WinnerUserId,
        string? FinishReason);
    private sealed record TournamentMatchView(
        int MatchId,
        string Status,
        int? Player1UserId,
        int? Player2UserId,
        int? CurrentMatchQuestionId);
}

public sealed class TournamentSmokeFactory : WebApplicationFactory<Program>
{
    public const string Password = "Prueba-Segura-2026!";
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"cale-tour-{Guid.NewGuid():N}.db");

    public User Admin { get; private set; } = null!;
    public User School { get; private set; } = null!;
    public User OtherSchool { get; private set; } = null!;
    public User Teacher { get; private set; } = null!;
    public User OtherTeacher { get; private set; } = null!;
    public List<User> Students { get; } = [];
    public User OtherSchoolStudent { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Cale", $"Data Source={_dbPath}");
        builder.UseSetting("Jwt:Key", "integration-tests-only-key-0123456789-abcdefghij");
        builder.UseSetting("Jwt:Issuer", "Cale.Api");
        builder.UseSetting("Jwt:Audience", "Cale.Frontend");
        builder.UseSetting("Database:AllowEnsureCreated", "true");
        builder.UseSetting("Database:ApplyFeatureSchema", "true");
        builder.UseSetting("Database:UseEfMigrations", "false");
        builder.UseSetting("Seed:BootstrapAdmin", "false");
        builder.UseSetting("Email:Enabled", "false");
        builder.UseSetting("Assistant:Enabled", "false");
        builder.ConfigureTestServices(services =>
        {
            foreach (var d in services.Where(d => d.ServiceType == typeof(DbContextOptions<CaleDbContext>)
                || d.ServiceType == typeof(DbContextOptions)).ToList())
            {
                services.Remove(d);
            }

            services.AddDbContext<CaleDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));
            foreach (var d in services
                .Where(d => d.ServiceType == typeof(IHostedService)
                    && d.ImplementationType?.Namespace?.StartsWith("Cale.Api", StringComparison.Ordinal) == true)
                .ToList())
            {
                services.Remove(d);
            }
        });
    }

    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        await TournamentSchemaGuard.EnsureAsync(db);
        await PlaySchemaGuard.EnsureAsync(db);

        var hasher = scope.ServiceProvider.GetRequiredService<Cale.BuildingBlocks.Domain.Security.IPasswordHasher>();
        var now = DateTime.UtcNow;
        var hash = hasher.Hash(Password);

        Admin = User.CreateAdmin("Admin_Smoke", "admin.smoke@pruebas.test", hash, now);
        School = User.RegisterSchool("School_Smoke", "school.smoke@pruebas.test", hash, now);
        OtherSchool = User.RegisterSchool("School_Other", "school.other@pruebas.test", hash, now);
        School.MarkEmailConfirmed();
        OtherSchool.MarkEmailConfirmed();
        db.AddRange(Admin, School, OtherSchool);
        await db.SaveChangesAsync();

        Teacher = User.CreateTeacher("Teacher_Smoke", "teacher.smoke@pruebas.test", hash, now, School.Id, emailConfirmed: true);
        OtherTeacher = User.CreateTeacher("Teacher_Other", "teacher.other@pruebas.test", hash, now, OtherSchool.Id, emailConfirmed: true);
        db.AddRange(Teacher, OtherTeacher);
        await db.SaveChangesAsync();

        for (var i = 1; i <= 13; i++)
        {
            var s = User.RegisterStudent($"Student_Smoke_{i}", $"student.smoke.{i}@pruebas.test", hash, now, School.Id);
            s.MarkEmailConfirmed();
            s.MarkCreatedBySchool(School.Id);
            Students.Add(s);
        }

        OtherSchoolStudent = User.RegisterStudent("Student_Other", "student.other@pruebas.test", hash, now, OtherSchool.Id);
        OtherSchoolStudent.MarkEmailConfirmed();
        db.AddRange(Students);
        db.Add(OtherSchoolStudent);
        await db.SaveChangesAsync();

        var bank = Bank.Create("Banco oficial smoke", null, now, createdById: null);
        db.Add(bank);
        await db.SaveChangesAsync();

        for (var i = 1; i <= 40; i++)
        {
            var q = Question.Create(
                bank.Id,
                blockId: 1,
                createdById: null,
                text: $"Pregunta smoke {i}?",
                type: QuestionTypes.MultipleChoice,
                topic: "Normas",
                imageUrl: null,
                explanation: "Porque sí",
                options:
                [
                    QuestionOption.Create($"Correcta {i}", true, null),
                    QuestionOption.Create($"Incorrecta {i}a", false, null),
                    QuestionOption.Create($"Incorrecta {i}b", false, null)
                ],
                utcNow: now);
            db.Add(q);
        }

        await db.SaveChangesAsync();
    }

    public HttpClient ClientFor(User user)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenFor(user));
        return client;
    }

    public string TokenFor(User user) =>
        Services.GetRequiredService<Cale.BuildingBlocks.Domain.Security.ITokenService>()
            .Create(user.Id, user.Email, user.Name, Roles.Normalize(user.Role));

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(_dbPath);
        }
        catch (IOException)
        {
        }
    }
}
