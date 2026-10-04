using System.Text.Json;
using Cale.BuildingBlocks.Domain.Catalog;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Scoring;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Catalog.Domain;
using Cale.Modules.Catalog.Infrastructure;

namespace Cale.UnitTests.Assessment;

public sealed class OfficialExamTests
{
    private static readonly string[] Scale = ["Muy en desacuerdo", "En desacuerdo", "De acuerdo", "Muy de acuerdo"];

    [Fact]
    public void Blueprint_MatchesTheOfficialResolution()
    {
        Assert.Equal(40, OfficialExam.TotalQuestions);
        Assert.Equal(12, OfficialExam.Sections.Single(s => s.Name == OfficialExam.Attitudes).Count);
        Assert.Equal(10, OfficialExam.Sections.Single(s => s.Name == OfficialExam.Mobility).Count);
        Assert.Equal(28, OfficialExam.Sections.Where(s => s.Name != OfficialExam.Attitudes).Sum(s => s.Count));
    }

    [Fact]
    public void Blueprint_KnowledgeSectionsAreCurriculumNuclei()
    {
        foreach (var section in OfficialExam.Sections.Where(s => s.Name != OfficialExam.Attitudes))
        {
            Assert.Contains(CurriculumTree.Nuclei, n => n.Name == section.Name);
        }
    }

    [Theory]
    [InlineData(QuestionTypes.Attitude, null, "Cualquiera", OfficialExam.Attitudes)]
    [InlineData(QuestionTypes.MultipleChoice, "El vehículo", "Normas de tránsito (Colombia)", OfficialExam.Vehicle)]
    [InlineData(QuestionTypes.MultipleChoice, "Normas de tránsito", "Normas de tránsito (Colombia)", OfficialExam.Rules)]
    [InlineData(QuestionTypes.MultipleChoice, "Señales preventivas", "Reconocimiento visual de señales", OfficialExam.Signs)]
    [InlineData(QuestionTypes.MultipleChoice, "Señales - acción", "Señales: qué debes hacer", OfficialExam.Signs)]
    [InlineData(QuestionTypes.MultipleChoice, null, "Movilidad segura y sostenible (CALE)", OfficialExam.Mobility)]
    [InlineData(QuestionTypes.MultipleChoice, "Motocicleta (A2)", "Normas de tránsito (Colombia)", null)]
    [InlineData(QuestionTypes.MultipleChoice, null, "Banco del profe", null)]
    public void SectionOf_UsesNucleusFirstThenBankName(string type, string? subject, string bank, string? expected)
    {
        Assert.Equal(expected, OfficialExam.SectionOf(type, subject, bank));
    }

    [Fact]
    public void Score_GradesKnowledgeAndAttitudesApart()
    {
        var answers = new List<(string?, bool)>();
        answers.AddRange(Enumerable.Range(0, 28).Select(i => ((string?)OfficialExam.Rules, i < 23)));
        answers.AddRange(Enumerable.Range(0, 12).Select(i => ((string?)OfficialExam.Attitudes, i < 10)));

        var score = OfficialExam.Score(answers);

        Assert.Equal(23, score.KnowledgeCorrect);
        Assert.Equal(28, score.KnowledgeTotal);
        Assert.Equal(10, score.AttitudeCorrect);
        Assert.Equal(12, score.AttitudeTotal);
        Assert.True(score.Passed);
    }

    [Theory]
    [InlineData(23, 10, true)]
    [InlineData(22, 12, false)]
    [InlineData(28, 9, false)]
    public void IsOfficialPassed_NeedsEightyPercentInBothParts(int knowledge, int attitudes, bool expected)
    {
        Assert.Equal(expected, ScoringRules.IsOfficialPassed(knowledge, 28, attitudes, 12));
    }

    [Fact]
    public void AttitudeQuestion_AllowsTwoCorrectAnswersButNotAll()
    {
        var ok = Attitude([true, true, false, false]);
        Assert.Equal(2, ok.Options.Count(o => o.IsCorrect));

        Assert.Throws<DomainException>(() => Attitude([true, true, true, true]));
        Assert.Throws<DomainException>(() => Attitude([false, false, false, false]));
    }

    [Fact]
    public void MultipleChoice_StillNeedsExactlyOneCorrectAnswer()
    {
        Assert.Throws<DomainException>(() => Question.Create(
            1, 1, null, "Pregunta", QuestionTypes.MultipleChoice, null, null, null,
            [QuestionOption.Create("A", true, null), QuestionOption.Create("B", true, null)],
            DateTime.UtcNow));
    }

    [Fact]
    public void CaleBankFiles_AreValidAndCoverTheOfficialQuotas()
    {
        var questions = CatalogSeed.CaleBankFiles
            .SelectMany(file => Load(file).Questions.Select(q => (File: file, Question: q)))
            .ToList();

        foreach (var (file, q) in questions)
        {
            var created = Question.Create(
                1, 1, null, q.Text, q.Type, q.Topic, q.ImageUrl, q.Explanation,
                q.Options.Select(o => QuestionOption.Create(o.Text, o.IsCorrect, null)).ToList(),
                DateTime.UtcNow);
            Assert.False(string.IsNullOrWhiteSpace(q.Explanation), $"{file}: {q.Text}");
            Assert.False(string.IsNullOrWhiteSpace(q.Source), $"{file}: {q.Text}");

            if (QuestionTypes.IsAttitude(created.Type))
            {
                Assert.Equal(Scale, q.Options.Select(o => o.Text));
                Assert.Equal(2, q.Options.Count(o => o.IsCorrect));
                var safeSide = q.Options[0].IsCorrect ? new[] { 0, 1 } : new[] { 2, 3 };
                Assert.All(safeSide, i => Assert.True(q.Options[i].IsCorrect, q.Text));
            }
            else
            {
                Assert.Equal(3, q.Options.Count);
                Assert.True(
                    CurriculumTree.Contains(q.Subject!, q.Topic!, q.Subtopic!),
                    $"{file}: {q.Subject} / {q.Topic} / {q.Subtopic}");
            }
        }

        Assert.Equal(questions.Count, questions.Select(x => x.Question.Text).Distinct().Count());
        Assert.True(questions.Count(x => QuestionTypes.IsAttitude(x.Question.Type)) >= 3 * 12);
        Assert.True(questions.Count(x => x.Question.Subject == OfficialExam.Mobility) >= 3 * 10);
        Assert.True(questions.Count(x => x.Question.Subject == OfficialExam.Vehicle) >= 3 * 6);
    }

    private static Question Attitude(bool[] correct) =>
        Question.Create(
            1, 1, null, "Enunciado", QuestionTypes.Attitude, "Actitudes", null, null,
            Scale.Select((text, i) => QuestionOption.Create(text, correct[i], null)).ToList(),
            DateTime.UtcNow);

    private static SeedFile Load(string file)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "SeedData", file);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SeedFile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private sealed record SeedFile(string BankName, string BlockName, List<SeedQuestion> Questions);

    private sealed record SeedQuestion(
        string Text,
        string Type,
        string? Subject,
        string? Topic,
        string? Subtopic,
        string? ImageUrl,
        string? Explanation,
        string? Source,
        List<SeedOption> Options);

    private sealed record SeedOption(string Text, bool IsCorrect);
}
