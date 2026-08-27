namespace Cale.Modules.Identity.Domain;

public static class SchoolPlans
{
    public const string Deferred = "Deferred";
    public const string Monthly = "Monthly";
    public const string Semestral = "Semestral";
    public const string Annual = "Annual";
    public const string Trial = "Trial";

    /// <summary>Account only — no product access until a paid plan or trial is activated.</summary>
    public static readonly SchoolPlanInfo DeferredPlan =
        new(Deferred, "Solo cuenta (sin pagar)", 0m, 0m, 0, MaxTeachers: 0, MaxStudents: 0);

    public static readonly SchoolPlanInfo TrialPlan =
        new(Trial, "Prueba gratis 1 mes", 0m, 0m, 1, MaxTeachers: 5, MaxStudents: 50);

    public static readonly IReadOnlyList<SchoolPlanInfo> All =
    [
        DeferredPlan,
        TrialPlan,
        new(Monthly, "Mensual", 150_000m, 150_000m, 1, MaxTeachers: 5, MaxStudents: 50),
        new(Semestral, "Semestral", 800_000m, 133_333.33m, 6, MaxTeachers: 12, MaxStudents: 150),
        new(Annual, "Anual", 1_500_000m, 125_000m, 12, MaxTeachers: 25, MaxStudents: 400)
    ];

    public static readonly IReadOnlyList<SchoolPlanInfo> PaidOnly =
    [
        All.First(x => x.Code == Monthly),
        All.First(x => x.Code == Semestral),
        All.First(x => x.Code == Annual)
    ];

    public static SchoolPlanInfo? Find(string? code)
    {
        var normalized = Normalize(code);
        return All.FirstOrDefault(x => x.Code == normalized);
    }

    public static string Normalize(string? code) => code switch
    {
        "Deferred" or "deferred" or "None" or "AccountOnly" or "SinPlan" or Deferred => Deferred,
        "Mensual" or "monthly" or Monthly => Monthly,
        "Semestral" or "semestral" or "Semester" => Semestral,
        "Anual" or "annual" or "Yearly" or Annual => Annual,
        "Trial" or "trial" or "FreeTrial" or Trial => Trial,
        _ => ""
    };

    public static bool IsValid(string? code) => Find(code) is not null;

    public static bool IsPaidPlan(string? code)
    {
        var n = Normalize(code);
        return n is Monthly or Semestral or Annual;
    }
}

public sealed record SchoolPlanInfo(
    string Code,
    string LabelEs,
    decimal PriceCop,
    decimal MonthlyEquivalentCop,
    int DurationMonths,
    int MaxTeachers,
    int MaxStudents);
