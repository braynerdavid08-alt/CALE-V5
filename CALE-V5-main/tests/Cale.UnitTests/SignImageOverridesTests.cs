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
    private static readonly SignDto[] Signs =
    [
        new("SR-01", "Señales reglamentarias", "Pare", "/signals/SR-01.svg"),
        new("SR-26", "Señales reglamentarias", "Prohibido adelantar", "/signals/SR-26.svg"),
        new("SR-26A", "Señales reglamentarias", "Fin de la prohibición de adelantar", "/signals/SR-26A.svg"),
        new("SP-01", "Señales preventivas", "Curva cerrada a la izquierda", "/signals/SP-01.svg"),
        new("SP-02", "Señales preventivas", "Curva cerrada a la derecha", "/signals/SP-02.svg")
    ];

    private static string? One(string answer) =>
        SignImageOverrides.Match(answer, Signs) is [var sign] ? sign.Code : null;

    [Theory]
    [InlineData("PARE", "SR-01")]
    [InlineData("  Pare. ", "SR-01")]
    [InlineData("FIN DE LA PROHIBICIÓN DE ADELANTAR", "SR-26A")]
    [InlineData("Fin de la prohibicion de adelantar", "SR-26A")]
    [InlineData("SR-26: prohibido adelantar", "SR-26")]
    [InlineData("SR 1", "SR-01")]
    [InlineData("¿Cuál de estas imágenes es la señal de curva cerrada a la derecha?", "SP-02")]
    [InlineData("Curva cerrada izquierda", "SP-01")]
    public void Matches_answers_to_catalog_signs(string answer, string code) =>
        Assert.Equal(code, One(answer));

    private sealed class SignsModelKey : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) => (context.GetType(), nameof(SignsModelKey), designTime);
    }

    [Fact]
    public async Task Build_uses_admin_sign_exams_only_and_reports_unmatched_answers()
    {
        var now = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
        await using var db = new CaleDbContext(
            new DbContextOptionsBuilder<CaleDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ReplaceService<IModelCacheKeyFactory, SignsModelKey>()
                .Options,
            new MappingAssemblies(typeof(UserConfiguration).Assembly, typeof(QuestionOptionConfiguration).Assembly));
        var admin = User.CreateAdmin("Admin", "admin@test.co", "x", now);
        var school = User.RegisterSchool("Escuela", "e@test.co", "x", now);
        var bank = Bank.Create("Señales", null, now);
        var block = Block.Create("Señales");
        db.AddRange(admin, school, bank, block);
        await db.SaveChangesAsync();

        Question Ask(string image, string answer) => Question.Create(bank.Id, block.Id, admin.Id, "¿Cómo se llama esta señal?",
            "Seleccion multiple", null, image, null,
            [QuestionOption.Create("Otra", false, null), QuestionOption.Create(answer, true, null)], now);
        var pare = Ask("/api/media/aaaa", "PARE");
        var unknown = Ask("/api/media/bbbb", "Semáforo peatonal");
        var curve = Ask("/api/media/cccc", "Curva cerrada a la derecha");
        var fake = Ask("/api/media/dddd", "PARE");
        db.AddRange(pare, unknown, curve, fake);
        var sr = Exam.Create("Examen Señales SR", null, bank.Id, 2, 10, 1, false, admin.Id, null, null, now);
        var sp = Exam.Create("Examen Señales Preventivas", null, bank.Id, 1, 10, 1, false, admin.Id, null, null, now);
        var foreign = Exam.Create("Examen señales escuela", null, bank.Id, 1, 10, 1, false, school.Id, null, null, now);
        db.AddRange(sr, sp, foreign);
        await db.SaveChangesAsync();

        void Link(Exam exam, Question question)
        {
            var link = (ExamQuestion)Activator.CreateInstance(typeof(ExamQuestion), true)!;
            db.Add(link);
            db.Entry(link).Property(nameof(ExamQuestion.ExamId)).CurrentValue = exam.Id;
            db.Entry(link).Property(nameof(ExamQuestion.QuestionId)).CurrentValue = question.Id;
        }
        Link(sr, pare);
        Link(sr, unknown);
        Link(sp, curve);
        Link(foreign, fake);
        await db.SaveChangesAsync();

        var report = await SignImageOverrides.BuildAsync(db, Signs, default);

        Assert.Equal(2, report.Exams.Count);
        Assert.Equal("/api/media/aaaa", Assert.Single(report.Matched, m => m.Code == "SR-01").ImageUrl);
        Assert.Equal("/api/media/cccc", Assert.Single(report.Matched, m => m.Code == "SP-02").ImageUrl);
        Assert.Equal(unknown.Id, Assert.Single(report.Unmatched).QuestionId);
        Assert.Contains("SR-26", report.MissingCodes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Semáforo peatonal")]
    [InlineData("Curva cerrada")]
    public void Does_not_guess_when_the_answer_is_unknown_or_ambiguous(string answer) =>
        Assert.Null(One(answer));
}
