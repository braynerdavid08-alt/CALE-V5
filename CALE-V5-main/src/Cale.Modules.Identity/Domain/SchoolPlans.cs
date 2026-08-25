namespace Cale.Modules.Identity.Domain;

public static class SchoolPlans
{
    public const string Monthly = "Monthly";
    public const string Semestral = "Semestral";
    public const string Annual = "Annual";

    public static readonly IReadOnlyList<SchoolPlanInfo> All =
    [
        new(Monthly, "Mensual", 150_000m, 150_000m, 1, MaxTeachers: 5, MaxStudents: 50),
        new(Semestral, "Semestral", 800_000m, 133_333.33m, 6, MaxTeachers: 12, MaxStudents: 150),
        new(Annual, "Anual", 1_500_000m, 125_000m, 12, MaxTeachers: 25, MaxStudents: 400)
    ];

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
}

public sealed record SchoolPlanInfo(
    string Code,
    string LabelEs,
    decimal PriceCop,
    decimal MonthlyEquivalentCop,
    int DurationMonths,
    int MaxTeachers,
    int MaxStudents);
