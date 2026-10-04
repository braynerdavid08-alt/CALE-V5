namespace Cale.BuildingBlocks.Domain.Catalog;

public static class QuestionTypes
{
    public const string MultipleChoice = "Seleccion multiple";
    public const string TrueFalse = "Verdadero/Falso";

    /// <summary>
    /// Likert statement of the official attitudes test: the scale keeps its order and
    /// both answers on the safe side of the scale count as correct.
    /// </summary>
    public const string Attitude = "Actitud";

    public static bool IsValid(string? type) =>
        type is MultipleChoice or TrueFalse or Attitude;

    public static bool IsAttitude(string? type) => type == Attitude;
}
