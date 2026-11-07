using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cale.BuildingBlocks.Domain.Catalog;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Domain.Gamification;
using Cale.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cale.UnitTests.Tournaments;

public sealed class TournamentCreateCapacityBanksTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private TournamentSmokeFactory _api = null!;
    private int _officialBankId;
    private int _examBankId;

    public async Task InitializeAsync()
    {
        _api = new TournamentSmokeFactory();
        _ = _api.Server;
        await _api.SeedAsync(studentCount: 4);
        await SeedExamBankAsync();
    }

    public Task DisposeAsync()
    {
        _api.Dispose();
        return Task.CompletedTask;
    }

    private async Task SeedExamBankAsync()
    {
        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var now = DateTime.UtcNow;

        _officialBankId = await db.Set<Bank>().AsNoTracking()
            .Where(b => b.CreatedById == null)
            .Select(b => b.Id)
            .FirstAsync();

        var examBank = Bank.Create("Banco examen teoría", null, now, createdById: _api.Teacher.Id);
        db.Add(examBank);
        await db.SaveChangesAsync();
        _examBankId = examBank.Id;

        for (var i = 1; i <= 12; i++)
        {
            db.Add(Question.Create(
                examBank.Id,
                blockId: 1,
                createdById: _api.Teacher.Id,
                text: $"Pregunta examen {i}?",
                type: QuestionTypes.MultipleChoice,
                topic: "Examen",
                imageUrl: null,
                explanation: "Clave examen",
                options:
                [
                    QuestionOption.Create("Sí", true, null),
                    QuestionOption.Create("No", false, null)
                ],
                utcNow: now));
        }

        await db.SaveChangesAsync();

        var exam = Exam.Create(
            name: "Examen teórico escuela",
            description: null,
            bankId: examBank.Id,
            questionCount: 12,
            timeMinutes: 30,
            allowedAttempts: 2,
            randomize: true,
            createdById: _api.Teacher.Id,
            startsAt: null,
            endsAt: null,
            utcNow: now);
        exam.SetPublished(true, now);
        db.Add(exam);
        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(13)]
    [InlineData(20)]
    public async Task Create_accepts_flexible_capacity_between_4_and_20(int capacity)
    {
        using var teacher = _api.ClientFor(_api.Teacher);
        var res = await teacher.PostAsJsonAsync("/api/tournaments", new
        {
            name = $"Copa {capacity}",
            scheduledAt = DateTime.UtcNow.AddHours(1),
            capacity,
            secondsPerQuestion = 30,
            questionMode = "random"
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<Created>(Json);
        Assert.NotNull(body);
        Assert.Equal(capacity, body.Capacity);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(21)]
    public async Task Create_rejects_capacity_outside_4_to_20(int capacity)
    {
        using var teacher = _api.ClientFor(_api.Teacher);
        var res = await teacher.PostAsJsonAsync("/api/tournaments", new
        {
            name = "Copa inválida",
            scheduledAt = DateTime.UtcNow.AddHours(1),
            capacity,
            secondsPerQuestion = 30,
            questionMode = "random"
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("invalid_capacity", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Question_banks_lists_official_and_exam_banks()
    {
        using var teacher = _api.ClientFor(_api.Teacher);
        var banks = await teacher.GetFromJsonAsync<List<BankDto>>("/api/tournaments/question-banks", Json);
        Assert.NotNull(banks);
        Assert.Contains(banks, b => b.Id == _officialBankId && b.Kind == "official");
        Assert.Contains(banks, b => b.Id == _examBankId && b.Kind == "exam");
    }

    [Fact]
    public async Task Create_with_exam_bank_persists_bank_ids()
    {
        using var teacher = _api.ClientFor(_api.Teacher);
        var res = await teacher.PostAsJsonAsync("/api/tournaments", new
        {
            name = "Copa banco examen",
            scheduledAt = DateTime.UtcNow.AddHours(1),
            capacity = 8,
            secondsPerQuestion = 25,
            questionMode = "random",
            bankIds = new[] { _examBankId }
        });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<Created>(Json);
        Assert.NotNull(body);

        using var scope = _api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
        var row = await db.Set<Tournament>().AsNoTracking().FirstAsync(t => t.Id == body.Id);
        Assert.Contains(_examBankId.ToString(), row.BankIdsJson);
    }

    [Fact]
    public async Task Create_rejects_bank_from_another_school()
    {
        int foreignBankId;
        using (var scope = _api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CaleDbContext>();
            var bank = Bank.Create("Banco otra escuela", null, DateTime.UtcNow, createdById: _api.OtherTeacher.Id);
            db.Add(bank);
            await db.SaveChangesAsync();
            foreignBankId = bank.Id;
        }

        using var teacher = _api.ClientFor(_api.Teacher);
        var res = await teacher.PostAsJsonAsync("/api/tournaments", new
        {
            name = "Copa banco ajeno",
            scheduledAt = DateTime.UtcNow.AddHours(1),
            capacity = 8,
            secondsPerQuestion = 25,
            questionMode = "random",
            bankIds = new[] { foreignBankId }
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("invalid_bank", await res.Content.ReadAsStringAsync());
    }

    private sealed record Created(int Id, int Capacity);
    private sealed record BankDto(int Id, string Name, string Kind);
}
