using Cale.BuildingBlocks.Domain.Catalog;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Catalog.Domain;

namespace Cale.UnitTests.Catalog;

public sealed class QuestionOwnershipTests
{
    [Fact]
    public void CanEdit_AllowsOwnerAndAdminOnly()
    {
        var question = Question.Create(
            bankId: 1,
            blockId: 1,
            createdById: 10,
            text: "¿Cuál es la velocidad máxima en zona escolar?",
            type: QuestionTypes.MultipleChoice,
            topic: "Normas",
            imageUrl: null,
            explanation: null,
            options:
            [
                QuestionOption.Create("30 km/h", true, null),
                QuestionOption.Create("60 km/h", false, null)
            ],
            utcNow: DateTime.UtcNow);

        Assert.True(question.CanEdit(10, isAdmin: false));
        Assert.False(question.CanEdit(99, isAdmin: false));
        Assert.True(question.CanEdit(99, isAdmin: true));
    }

    [Fact]
    public void Validate_RejectsUnknownQuestionType()
    {
        Assert.Throws<DomainException>(() => Question.Create(
            bankId: 1,
            blockId: 1,
            createdById: 1,
            text: "Pregunta",
            type: "otro-tipo",
            topic: null,
            imageUrl: null,
            explanation: null,
            options:
            [
                QuestionOption.Create("A", true, null),
                QuestionOption.Create("B", false, null)
            ],
            utcNow: DateTime.UtcNow));
    }
}
