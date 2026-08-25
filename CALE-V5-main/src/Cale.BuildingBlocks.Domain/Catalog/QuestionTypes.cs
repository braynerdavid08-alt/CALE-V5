namespace Cale.BuildingBlocks.Domain.Catalog;

public static class QuestionTypes
{
    public const string MultipleChoice = "Seleccion multiple";
    public const string TrueFalse = "Verdadero/Falso";

    public static bool IsValid(string? type) =>
        type is MultipleChoice or TrueFalse;
}
