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
        new("SR-26", "Señales reglamentarias", "No adelantar", "/signals/SR-26.svg"),
        new("SR-26A", "Señales reglamentarias", "Fin de la prohibición de adelantar", "/signals/SR-26A.svg"),
        new("SR-28", "Señales reglamentarias", "Prohibido parquear", "/signals/SR-28.svg"),
        new("SP-01", "Señales preventivas", "Curva cerrada a la izquierda", "/signals/SP-01.svg"),
        new("SP-02", "Señales preventivas", "Curva cerrada a la derecha", "/signals/SP-02.svg"),
        new("SR-03", "Señales reglamentarias", "Dirección obligada o siga de frente", "/signals/SR-03.svg")
    ];

    private static string? One(string answer) =>
        SignImageOverrides.Match(answer, Signs) is [var sign] ? sign.Code : null;

    [Theory]
    [InlineData("PARE", "SR-01")]
    [InlineData("  Pare. ", "SR-01")]
    [InlineData("Señal de PARE", "SR-01")]
    [InlineData("La señal de Pare", "SR-01")]
    [InlineData("STOP", "SR-01")]
    [InlineData("FIN DE LA PROHIBICIÓN DE ADELANTAR", "SR-26A")]
    [InlineData("Fin de la prohibicion de adelantar", "SR-26A")]
    [InlineData("SR-26: prohibido adelantar", "SR-26")]
    [InlineData("PROHIBIDO ADELANTAR", "SR-26")]
    [InlineData("PROHIBIDO ESTACIONAR", "SR-28")]
    [InlineData("SIGA DE FRENTE", "SR-03")]
    [InlineData("SR 1", "SR-01")]
    [InlineData("SR01", "SR-01")]
    [InlineData("¿Cuál de estas imágenes es la señal de curva cerrada a la derecha?", "SP-02")]
    [InlineData("Curva cerrada izquierda", "SP-01")]
    public void Matches_answers_to_catalog_signs(string answer, string code) =>
        Assert.Equal(code, One(answer));

    [Theory]
    [InlineData("Examen Señales SR", true)]
    [InlineData("Reglamentarias oficiales", true)]
    [InlineData("Preventivas 2024", true)]
    [InlineData("Examen teoría general", false)]
    public void Detects_sign_exam_names(string name, bool expected) =>
        Assert.Equal(expected, SignImageOverrides.IsSignExamName(name));

    private sealed class SignsModelKey : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) => (context.GetType(), nameof(SignsModelKey), designTime);
    }

    [Fact]
    public async Task Build_uses_admin_exams_admin_banks_and_official_bank_questions()
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
        var official = Bank.Create("Señales oficiales", null, now);
        var schoolBank = Bank.Create("Banco escuela", null, now, school.Id);
        var block = Block.Create("Señales");
        db.AddRange(admin, school, official, schoolBank, block);
        await db.SaveChangesAsync();

        Question Ask(int bankId, int authorId, string image, string answer) => Question.Create(
            bankId, block.Id, authorId, "¿Cómo se llama esta señal?",
            "Seleccion multiple", null, image, null,
            [QuestionOption.Create("Otra", false, null), QuestionOption.Create(answer, true, null)], now);

        var pare = Ask(official.Id, admin.Id, "/api/media/aaaa", "PARE");
        var unknown = Ask(official.Id, admin.Id, "/api/media/bbbb", "Semáforo peatonal");
        var curve = Ask(official.Id, admin.Id, "/api/media/cccc", "Curva cerrada a la derecha");
        var schoolOfficialQ = Ask(official.Id, school.Id, "/api/media/eeee", "SR-26");
        var schoolPrivate = Ask(schoolBank.Id, school.Id, "/api/media/ffff", "PARE");
        // In the official bank but not linked to any exam — second pass must still map it.
        var orphanInBank = Ask(official.Id, admin.Id, "/api/media/gggg", "PROHIBIDO ESTACIONAR");
        db.AddRange(pare, unknown, curve, schoolOfficialQ, schoolPrivate, orphanInBank);

        var sr = Exam.Create("Examen Señales SR", null, official.Id, 2, 10, 1, false, admin.Id, null, null, now);
        var sp = Exam.Create("Examen Señales Preventivas", null, official.Id, 1, 10, 1, false, admin.Id, null, null, now);
        var schoolOfficialExam = Exam.Create(
            "Examen señales reglamentarias escuela", null, official.Id, 1, 10, 1, false, school.Id, null, null, now);
        var schoolPrivateExam = Exam.Create(
            "Examen señales interno", null, schoolBank.Id, 1, 10, 1, false, school.Id, null, null, now);
        db.AddRange(sr, sp, schoolOfficialExam, schoolPrivateExam);
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
        Link(schoolOfficialExam, schoolOfficialQ);
        Link(schoolPrivateExam, schoolPrivate);
        await db.SaveChangesAsync();

        var report = await SignImageOverrides.BuildAsync(db, Signs, default);

        Assert.Contains(report.Exams, n => n.Contains("SR", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Exams, n => n.Contains("Preventivas", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Exams, n => n.Contains("escuela", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(report.Exams, n => n.Contains("interno", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("/api/media/aaaa", Assert.Single(report.Matched, m => m.Code == "SR-01").ImageUrl);
        Assert.Equal("/api/media/cccc", Assert.Single(report.Matched, m => m.Code == "SP-02").ImageUrl);
        Assert.Equal("/api/media/eeee", Assert.Single(report.Matched, m => m.Code == "SR-26").ImageUrl);
        Assert.Equal("/api/media/gggg", Assert.Single(report.Matched, m => m.Code == "SR-28").ImageUrl);
        Assert.DoesNotContain(report.Matched, m => m.ImageUrl == "/api/media/ffff");
        Assert.Equal(unknown.Id, Assert.Single(report.Unmatched).QuestionId);
        Assert.Contains("SR-26A", report.MissingCodes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Semáforo peatonal")]
    [InlineData("Curva cerrada")]
    public void Does_not_guess_when_the_answer_is_unknown_or_ambiguous(string answer) =>
        Assert.Null(One(answer));
}
