namespace Cale.Modules.Catalog.Application;

public static class ExamImportMarkers
{
    public const string NeedsReviewExplanation =
        "Importada sin clave: revisa y marca la respuesta correcta antes de publicar.";

    public static bool NeedsReview(string? explanation) =>
        !string.IsNullOrWhiteSpace(explanation)
        && explanation.Contains("Importada sin clave", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Clears the Word-import review flag once the teacher marks a correct answer.
    /// </summary>
    public static string? ClearNeedsReview(string? explanation)
    {
        if (!NeedsReview(explanation))
        {
            return explanation;
        }

        var cleaned = explanation!
            .Replace(NeedsReviewExplanation, string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }
}
