using Cale.BuildingBlocks.Domain.Catalog;
using Cale.BuildingBlocks.Domain.Scoring;
using Cale.Modules.Catalog.Domain;

namespace Cale.Modules.Assessment.Domain;

/// <summary>
/// Structure of the official CALE theory exam (Res. 20253040037125 de 2025, art. 3.7.3.1.3):
/// 28 knowledge questions split by nucleus plus 12 attitude statements.
/// </summary>
public static class OfficialExam
{
    public const string Mobility = "Movilidad segura y sostenible";
    public const string Rules = "Normas de tránsito";
    public const string Signs = "Señalización e infraestructura vial";
    public const string Vehicle = "El vehículo";
    public const string Attitudes = "Actitudes";

    public const int TimeMinutes = 60;

    public static IReadOnlyList<OfficialSection> Sections { get; } =
    [
        new(Mobility, 10),
        new(Rules, 6),
        new(Signs, 6),
        new(Vehicle, 6),
        new(Attitudes, 12)
    ];

    public static int TotalQuestions { get; } = Sections.Sum(s => s.Count);

    private static readonly string[] KnowledgeSections = [Mobility, Rules, Signs, Vehicle];

    /// <summary>
    /// Section a question feeds in the official simulacro, or null when it does not belong to it.
    /// The curriculum nucleus wins; unclassified questions fall back to the bank name.
    /// </summary>
    public static string? SectionOf(string? type, string? subject, string? bankName)
    {
        if (QuestionTypes.IsAttitude(type))
        {
            return Attitudes;
        }

        var nucleus = subject?.Trim();
        if (!string.IsNullOrEmpty(nucleus))
        {
            var knowledge = KnowledgeSections.FirstOrDefault(s => Same(s, nucleus));
            if (knowledge is not null)
            {
                return knowledge;
            }

            if (CurriculumTree.Nuclei.Any(n => Same(n.Name, nucleus)))
            {
                return null;
            }

            if (nucleus.StartsWith("Señal", StringComparison.OrdinalIgnoreCase))
            {
                return Signs;
            }
        }

        var bank = bankName ?? "";
        if (Has(bank, "señal") || Has(bank, "senal"))
        {
            return Signs;
        }

        if (Has(bank, "norma"))
        {
            return Rules;
        }

        if (Has(bank, "vehículo") || Has(bank, "vehiculo"))
        {
            return Vehicle;
        }

        return Has(bank, "movilidad") ? Mobility : null;
    }

    public static OfficialScore Score(IEnumerable<(string? Section, bool IsCorrect)> answers)
    {
        var list = answers.Where(a => a.Section is not null).ToList();
        var bySection = Sections
            .Select(s =>
            {
                var rows = list.Where(a => a.Section == s.Name).ToList();
                return new OfficialSectionScore(s.Name, rows.Count(a => a.IsCorrect), rows.Count);
            })
            .Where(s => s.Total > 0)
            .ToList();
        var knowledge = bySection.Where(s => s.Name != Attitudes).ToList();
        var attitudes = bySection.Where(s => s.Name == Attitudes).ToList();
        return new OfficialScore(
            knowledge.Sum(s => s.Correct),
            knowledge.Sum(s => s.Total),
            attitudes.Sum(s => s.Correct),
            attitudes.Sum(s => s.Total),
            bySection);
    }

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool Has(string text, string fragment) =>
        text.Contains(fragment, StringComparison.OrdinalIgnoreCase);
}

public sealed record OfficialSection(string Name, int Count);

public sealed record OfficialSectionScore(string Name, int Correct, int Total);

public sealed record OfficialScore(
    int KnowledgeCorrect,
    int KnowledgeTotal,
    int AttitudeCorrect,
    int AttitudeTotal,
    IReadOnlyList<OfficialSectionScore> Sections)
{
    public bool Passed =>
        ScoringRules.IsOfficialPassed(KnowledgeCorrect, KnowledgeTotal, AttitudeCorrect, AttitudeTotal);
}
