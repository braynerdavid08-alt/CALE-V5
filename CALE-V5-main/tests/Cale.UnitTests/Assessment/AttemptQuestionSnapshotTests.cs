using Cale.BuildingBlocks.Domain.Catalog;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Catalog.Domain;

namespace Cale.UnitTests.Assessment;

public sealed class AttemptQuestionSnapshotTests
{
    [Fact]
    public void Serialize_RoundTripsQuestionContentAndOptionOrder()
    {
        var question = Question.Create(
            bankId: 1,
            blockId: 2,
            createdById: 3,
            text: "¿Quién tiene prioridad?",
            type: QuestionTypes.MultipleChoice,
            topic: "Normas",
            imageUrl: null,
            explanation: "Por la derecha",
            options:
            [
                QuestionOption.Create("El de la derecha", true, null),
                QuestionOption.Create("El más grande", false, null),
                QuestionOption.Create("Nadie", false, null)
            ],
            utcNow: DateTime.UtcNow);

        var presented = question.Options.Reverse().ToList();
        var snapshot = AttemptQuestionSnapshot.FromQuestion(question, presented);
        var json = AttemptQuestionSnapshot.Serialize(snapshot);
        var parsed = AttemptQuestionSnapshot.TryParse(json);

        Assert.NotNull(parsed);
        Assert.Equal("¿Quién tiene prioridad?", parsed!.Text);
        Assert.Equal("Normas", parsed.Topic);
        Assert.Equal(2, parsed.BlockId);
        Assert.Equal(3, parsed.Options.Count);
        Assert.Equal(presented[0].Id, parsed.Options[0].Id);
        Assert.Equal(presented[0].Text, parsed.Options[0].Text);
        Assert.Equal("El de la derecha", parsed.CorrectText());
        Assert.Null(parsed.Section);
    }

    [Fact]
    public void AttitudeSnapshot_ListsEveryAcceptedAnswerAndKeepsSection()
    {
        var question = Question.Create(
            bankId: 1,
            blockId: 2,
            createdById: null,
            text: "Conduzco por el andén cuando no hay peatones.",
            type: QuestionTypes.Attitude,
            topic: "Actitudes",
            imageUrl: null,
            explanation: null,
            options:
            [
                QuestionOption.Create("Muy en desacuerdo", true, null),
                QuestionOption.Create("En desacuerdo", true, null),
                QuestionOption.Create("De acuerdo", false, null),
                QuestionOption.Create("Muy de acuerdo", false, null)
            ],
            utcNow: DateTime.UtcNow);

        var snapshot = AttemptQuestionSnapshot.FromQuestion(question, question.Options.ToList(), "Actitudes");
        var parsed = AttemptQuestionSnapshot.TryParse(AttemptQuestionSnapshot.Serialize(snapshot));

        Assert.Equal("Muy en desacuerdo o En desacuerdo", parsed!.CorrectText());
        Assert.Equal("Actitudes", parsed.Section);
    }

    [Fact]
    public void TryParse_ReturnsNull_ForEmptyJson()
    {
        Assert.Null(AttemptQuestionSnapshot.TryParse(null));
        Assert.Null(AttemptQuestionSnapshot.TryParse("{}"));
        Assert.Null(AttemptQuestionSnapshot.TryParse("not-json"));
    }
}
