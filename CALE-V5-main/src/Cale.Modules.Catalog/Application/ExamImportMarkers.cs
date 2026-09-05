namespace Cale.Modules.Catalog.Application;

public static class ExamImportMarkers
{
    public const string NeedsReviewExplanation =
        "Importada sin clave: revisa y marca la respuesta correcta antes de publicar.";

    public static bool NeedsReview(string? explanation) =>
        !string.IsNullOrWhiteSpace(explanation)
        && explanation.Contains("Importada sin clave", StringComparison.OrdinalIgnoreCase);
}
