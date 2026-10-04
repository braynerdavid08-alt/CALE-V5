using System.Net.Http.Headers;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.BuildingBlocks.Infrastructure.Security;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Catalog.Domain;
using Cale.Modules.Classroom.Domain;
using Cale.Modules.Identity.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Cale.UnitTests.Security.Integration;

/// <summary>
/// Boots the real API (full middleware pipeline, real controllers, real authorization) on a throwaway
/// SQLite file and seeds the attack-matrix accounts: two schools, each with a teacher and students,
/// plus a global admin. Nothing touches production data.
/// </summary>
public sealed class SecurityApiFactory : WebApplicationFactory<Program>
{
    public const string JwtKey = "integration-tests-only-key-0123456789-abcdefghij";
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"cale-sec-{Guid.NewGuid():N}.db");
    private readonly IReadOnlyDictionary<string, string> _extraSettings;

    public SecurityApiFactory()
        : this(new Dictionary<string, string>())
    {
    }

    public SecurityApiFactory(IReadOnlyDictionary<string, string> extraSettings) => _extraSettings = extraSettings;

    public Accounts Users { get; private set; } = null!;
    public Fixtures Data { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" (not Development) so appsettings.Development.local.json — which may point at a real
        // database — is never loaded; the DbContext is also forced onto the temp SQLite file below.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Cale", $"Data Source={_dbPath}");
        builder.UseSetting("Jwt:Key", JwtKey);
        builder.UseSetting("Jwt:Issuer", "Cale.Api");
        builder.UseSetting("Jwt:Audience", "Cale.Frontend");
        builder.UseSetting("Database:AllowEnsureCreated", "true");
        builder.UseSetting("Database:ApplyFeatureSchema", "true");
        builder.UseSetting("Database:UseEfMigrations", "false");
        builder.UseSetting("Seed:BootstrapAdmin", "false");
        builder.UseSetting("Email:Enabled", "false");
        builder.UseSetting("Assistant:Enabled", "false");
        builder.UseSetting("Security:Csp:Mode", "enforce");
        foreach (var (key, value) in _extraSettings)
        {
            builder.UseSetting(key, value);
        }
        builder.ConfigureTestServices(services =>
        {
            foreach (var d in services.Where(d => d.ServiceType == typeof(DbContextOptions<CaleDbContext>)
                || d.ServiceType == typeof(DbContextOptions)).ToList())
            {
                services.Remove(d);
            }
            services.AddDbContext<CaleDbContext>(o => o.UseSqlite($"Data Source={_dbPath}"));

            var background = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                    && d.ImplementationType?.Namespace?.StartsWith("Cale.Api", StringComparison.Ordinal) == true)
                .ToList();
            foreach (var d in background)
            {
                services.Remove(d);
            }
        });
    }

    public async Task SeedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var target = db.Database.GetConnectionString() ?? "";
        if (!db.Database.IsSqlite() || !target.Contains(_dbPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to seed: the test host is not using the throwaway SQLite database.");
        }
        var hasher = scope.ServiceProvider.GetRequiredService<Cale.BuildingBlocks.Domain.Security.IPasswordHasher>();
        var now = DateTime.UtcNow;
        var hash = hasher.Hash("Prueba-Segura-2026!");

        var admin = User.CreateAdmin("Admin_Global", "admin.global@pruebas.test", hash, now);
        var school1 = User.RegisterSchool("School_Admin_1", "school1@pruebas.test", hash, now);
        var school2 = User.RegisterSchool("School_Admin_2", "school2@pruebas.test", hash, now);
        school1.MarkEmailConfirmed();
        school2.MarkEmailConfirmed();
        db.AddRange(admin, school1, school2);
        await db.SaveChangesAsync();

        var teacher1 = User.CreateTeacher("Teacher_School_1", "teacher1@pruebas.test", hash, now, school1.Id, emailConfirmed: true);
        var teacher2 = User.CreateTeacher("Teacher_School_2", "teacher2@pruebas.test", hash, now, school2.Id, emailConfirmed: true);
        var studentA = User.RegisterStudent("Student_A_School_1", "student.a@pruebas.test", hash, now, school1.Id);
        var studentB = User.RegisterStudent("Student_B_School_1", "student.b@pruebas.test", hash, now, school1.Id);
        var studentC = User.RegisterStudent("Student_C_School_2", "student.c@pruebas.test", hash, now, school2.Id);
        foreach (var u in new[] { studentA, studentB, studentC })
        {
            u.MarkEmailConfirmed();
        }
        studentA.MarkCreatedBySchool(school1.Id);
        studentB.MarkCreatedBySchool(school1.Id);
        studentC.MarkCreatedBySchool(school2.Id);
        db.AddRange(teacher1, teacher2, studentA, studentB, studentC);
        await db.SaveChangesAsync();

        var privateBank = Bank.Create("Banco privado Teacher 2", null, now, createdById: teacher2.Id);
        db.Add(privateBank);
        await db.SaveChangesAsync();
        var privateQuestion = Question.Create(
            privateBank.Id,
            blockId: 1,
            createdById: teacher2.Id,
            text: "Pregunta privada de la escuela 2",
            type: Cale.BuildingBlocks.Domain.Catalog.QuestionTypes.MultipleChoice,
            topic: "Privado",
            imageUrl: null,
            explanation: "Solucionario privado",
            options: [QuestionOption.Create("Correcta", true, null), QuestionOption.Create("Incorrecta", false, null)],
            utcNow: now);
        db.Add(privateQuestion);

        var group2 = Group.Create("Grupo Teacher 2", null, teacher2.Id, null, now);
        db.Add(group2);

        var inactive = User.RegisterStudent("Student_Inactive_School_1", "student.inactive@pruebas.test", hash, now, school1.Id);
        inactive.MarkEmailConfirmed();
        inactive.Deactivate();
        db.Add(inactive);

        var loneInvited = User.RegisterStudent("Lone_Student_Invited", "lone.invited@pruebas.test", hash, now);
        var loneRejecting = User.RegisterStudent("Lone_Student_Rejecting", "lone.rejecting@pruebas.test", hash, now);
        var loneRequesting = User.RegisterStudent("Lone_Student_Requesting", "lone.requesting@pruebas.test", hash, now);
        var loneTeacher = User.CreateTeacher("Lone_Teacher", "lone.teacher@pruebas.test", hash, now, emailConfirmed: true);
        foreach (var u in new[] { loneInvited, loneRejecting, loneRequesting })
        {
            u.MarkEmailConfirmed();
        }
        db.AddRange(loneInvited, loneRejecting, loneRequesting, loneTeacher);
        db.Add(SchoolProfile.CreateDraft(school1.Id, school1.Name, school1.Email, SchoolPlans.Find(SchoolPlans.Monthly)!, now));

        var attemptA = Attempt.Start(studentA.Id, privateBank.Id, null, "practice", 10, 30, now);
        db.Add(attemptA);
        await db.SaveChangesAsync();

        // Student A has the question in an open exam and also in the practice "mistakes" list.
        db.Add(AttemptQuestion.Create(attemptA.Id, privateQuestion.Id, 1));
        db.Add(new Cale.Modules.Assessment.Domain.Gamification.MistakeReview
        {
            UserId = studentA.Id,
            QuestionId = privateQuestion.Id,
            NextDueAt = now,
            LastWrongAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        Users = new Accounts(
            admin, school1, school2, teacher1, teacher2, studentA, studentB, studentC, inactive,
            loneInvited, loneRejecting, loneRequesting, loneTeacher);
        Data = new Fixtures(privateBank.Id, privateQuestion.Id, group2.Id, attemptA.Id);
    }

    public const string Password = "Prueba-Segura-2026!";

    public HttpClient ClientFor(User? user)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
        if (user is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenFor(user));
        }

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

    public sealed record Accounts(
        User Admin,
        User School1,
        User School2,
        User Teacher1,
        User Teacher2,
        User StudentA,
        User StudentB,
        User StudentC,
        User InactiveStudent,
        User LoneInvitedStudent,
        User LoneRejectingStudent,
        User LoneRequestingStudent,
        User LoneTeacher);

    public sealed record Fixtures(int PrivateBankId, int PrivateQuestionId, int Teacher2GroupId, int StudentAAttemptId);
}

public sealed class SecurityApiFixture : IAsyncLifetime
{
    public SecurityApiFactory Factory { get; } = new();

    public async Task InitializeAsync()
    {
        _ = Factory.Server;
        await Factory.SeedAsync();
    }

    public Task DisposeAsync()
    {
        Factory.Dispose();
        return Task.CompletedTask;
    }
}

[CollectionDefinition(Name)]
public sealed class SecurityApiCollection : ICollectionFixture<SecurityApiFixture>
{
    public const string Name = "security-api";
}
