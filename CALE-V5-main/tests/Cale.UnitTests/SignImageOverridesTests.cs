using Cale.Api.Services.Play;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Catalog.Domain;
using Cale.Modules.Catalog.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Cale.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Cale.UnitTests;

public sealed class SignImageOverridesTests
{
    private static readonly SignDto[] Catalog =
    [
        new("SR-01", "Señales reglamentarias", "Pare", "/signals/SR-01.svg"),
        new("SR-26", "Señales reglamentarias", "No adelantar", "/signals/SR-26.svg"),
        new("SP-02", "Señales preventivas", "Curva cerrada a la derecha", "/signals/SP-02.svg"),
        new("SI-01", "Señales informativas", "Hospital", "/signals/SI-01.svg")
    ];

    private static string? One(string answer) =>
        SignImageOverrides.Match(answer, Catalog) is [var sign] ? sign.Code : null;

    [Theory]
    [InlineData("PARE", "SR-01")]
    [InlineData("SR-26", "SR-26")]
    [InlineData("Curva cerrada a la derecha", "SP-02")]
    public void Matches_answers_to_catalog_codes(string answer, string code) =>
        Assert.Equal(code, One(answer));

    private sealed class SignsModelKey : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) => (context.GetType(), nameof(SignsModelKey), designTime);
    }

    [Fact]
    public async Task Bank_is_built_only_from_the_three_template_exams_with_real_photos()
    {
        var now = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
        await using var db = new CaleDbContext(
            new DbContextOptionsBuilder<CaleDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ReplaceService<IModelCacheKeyFactory, SignsModelKey>()
                .Options,
            new MappingAssemblies(typeof(UserConfiguration).Assembly, typeof(QuestionOptionConfiguration).Assembly));
        var admin = User.CreateAdmin("Admin", "admin@test.co", "x", now);
        var bank = Bank.Create("Señales", null, now);
        var block = Block.Create("Señales");
        db.AddRange(admin, bank, block);
        await db.SaveChangesAsync();

        Question Ask(string image, string answer) => Question.Create(
            bank.Id, block.Id, admin.Id, "¿Cómo se llama esta señal?",
            "Seleccion multiple", null, image, null,
            [QuestionOption.Create("Otra", false, null), QuestionOption.Create(answer, true, null)], now);

        var pare = Ask("/api/media/pare", "PARE");
        var curve = Ask("/api/media/curve", "Curva cerrada a la derecha");
        var hospital = Ask("/api/media/hosp", "Hospital");
        var noImage = Ask("", "PARE");
        var other = Ask("/api/media/other", "Algo raro");
        db.AddRange(pare, curve, hospital, noImage, other);

        var sr = Exam.Create("Examen Señales Reglamentarias", null, bank.Id, 2, 10, 1, false, admin.Id, null, null, now);
        var sp = Exam.Create("Examen Señales Preventivas", null, bank.Id, 1, 10, 1, false, admin.Id, null, null, now);
        var si = Exam.Create("Examen Señales Informativas", null, bank.Id, 1, 10, 1, false, admin.Id, null, null, now);
        var noise = Exam.Create("Examen teoría general", null, bank.Id, 1, 10, 1, false, admin.Id, null, null, now);
        db.AddRange(sr, sp, si, noise);
        await db.SaveChangesAsync();

        void Link(Exam exam, Question question)
        {
            var link = (ExamQuestion)Activator.CreateInstance(typeof(ExamQuestion), true)!;
            db.Add(link);
            db.Entry(link).Property(nameof(ExamQuestion.ExamId)).CurrentValue = exam.Id;
            db.Entry(link).Property(nameof(ExamQuestion.QuestionId)).CurrentValue = question.Id;
        }

        Link(sr, pare);
        Link(sr, noImage);
        Link(sp, curve);
        Link(si, hospital);
        Link(noise, other);
        await db.SaveChangesAsync();

        var (bankSigns, report) = await SignImageOverrides.BuildBankAsync(db, Catalog, default);

        Assert.Equal(3, report.Exams.Count);
        Assert.Contains(report.Exams, n => n.Contains("Reglamentarias", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Exams, n => n.Contains("Preventivas", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Exams, n => n.Contains("Informativas", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(report.Exams, n => n.Contains("teoría", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(3, bankSigns.Count);
        Assert.All(bankSigns, s => Assert.DoesNotContain("/signals/", s.ImageUrl, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(bankSigns, s => s.Code == "SR-01" && s.ImageUrl == "/api/media/pare" && s.Name == "Pare");
        Assert.Contains(bankSigns, s => s.Code == "SP-02" && s.ImageUrl == "/api/media/curve");
        Assert.Contains(bankSigns, s => s.Family == "Señales informativas" && s.ImageUrl == "/api/media/hosp");
        Assert.DoesNotContain(bankSigns, s => s.ImageUrl == "/api/media/other");
    }
}
