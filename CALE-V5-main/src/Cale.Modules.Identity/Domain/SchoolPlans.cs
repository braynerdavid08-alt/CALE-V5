namespace Cale.Modules.Identity.Domain;

/// <summary>
/// Fallback catalog when CommercialPlans table is empty.
/// MaxTeachers/MaxStudents of 0 means unlimited (temporary commercial offer).
/// Admin-editable prices live in <see cref="CommercialPlan"/>.
/// </summary>
public static class SchoolPlans
{
    public const string Monthly = "Monthly";
    public const string Semestral = "Semestral";
    public const string Annual = "Annual";

    /// <summary>Commercial offer defaults (admin can override via CommercialPlans).</summary>
    public static readonly IReadOnlyList<SchoolPlanInfo> All =
    [
        new(Monthly, "Mensual", 500_000m, 500_000m, 1, MaxTeachers: 0, MaxStudents: 0),
        new(Annual, "Anual", 5_000_000m, 416_666.67m, 12, MaxTeachers: 0, MaxStudents: 0),
        // Legacy — kept for schools already on this code; hidden from new commercial UI.
        new(Semestral, "Semestral", 800_000m, 133_333.33m, 6, MaxTeachers: 0, MaxStudents: 0)
    ];

    public static IEnumerable<SchoolPlanInfo> CommercialOffer =>
        All.Where(x => x.Code is Monthly or Annual);

    public static SchoolPlanInfo? Find(string? code)
    {
        var normalized = Normalize(code);
        return All.FirstOrDefault(x => x.Code == normalized);
    }

    public static string Normalize(string? code) => code switch
    {
        "Mensual" or "monthly" or Monthly => Monthly,
        "Semestral" or "semestral" or "Semester" => Semestral,
        "Anual" or "annual" or "Yearly" or Annual => Annual,
        _ => ""
    };

    public static bool IsValid(string? code) => Find(code) is not null;

    /// <summary>0 (or negative) means unlimited seats for the temporary commercial model.</summary>
    public static bool HasSeatCap(int max) => max > 0;
}

public sealed record SchoolPlanInfo(
    string Code,
    string LabelEs,
    decimal PriceCop,
    decimal MonthlyEquivalentCop,
    int DurationMonths,
    int MaxTeachers,
    int MaxStudents);
