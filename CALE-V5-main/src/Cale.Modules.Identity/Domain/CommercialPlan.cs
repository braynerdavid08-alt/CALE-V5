namespace Cale.Modules.Identity.Domain;

/// <summary>
/// Admin-editable commercial plan. Null seat caps mean unlimited (documented for temporary B2B offer).
/// </summary>
public sealed class CommercialPlan
{
    public int Id { get; private set; }
    public string Code { get; private set; } = "";
    public string LabelEs { get; private set; } = "";
    public decimal PriceCop { get; private set; }
    public int DurationMonths { get; private set; }
    public int? MaxTeachers { get; private set; }
    public int? MaxStudents { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsLegacy { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private CommercialPlan()
    {
    }

    public static CommercialPlan Create(
        string code,
        string labelEs,
        decimal priceCop,
        int durationMonths,
        int? maxTeachers,
        int? maxStudents,
        bool isLegacy,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Código de plan requerido.", nameof(code));
        }

        if (priceCop < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(priceCop));
        }

        if (durationMonths is < 1 or > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMonths));
        }

        return new CommercialPlan
        {
            Code = SchoolPlans.Normalize(code),
            LabelEs = labelEs.Trim(),
            PriceCop = priceCop,
            DurationMonths = durationMonths,
            MaxTeachers = maxTeachers,
            MaxStudents = maxStudents,
            IsActive = true,
            IsLegacy = isLegacy,
            UpdatedAt = utcNow
        };
    }

    public void Update(
        string labelEs,
        decimal priceCop,
        int durationMonths,
        int? maxTeachers,
        int? maxStudents,
        bool isActive,
        DateTime utcNow)
    {
        if (priceCop < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(priceCop));
        }

        if (durationMonths is < 1 or > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(durationMonths));
        }

        if (maxTeachers is < 0 or > 100_000)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTeachers));
        }

        if (maxStudents is < 0 or > 100_000)
        {
            throw new ArgumentOutOfRangeException(nameof(maxStudents));
        }

        LabelEs = labelEs.Trim();
        PriceCop = priceCop;
        DurationMonths = durationMonths;
        MaxTeachers = maxTeachers;
        MaxStudents = maxStudents;
        IsActive = isActive;
        UpdatedAt = utcNow;
    }

    public SchoolPlanInfo ToPlanInfo()
    {
        var monthly = DurationMonths <= 0
            ? PriceCop
            : Math.Round(PriceCop / DurationMonths, 2, MidpointRounding.AwayFromZero);
        return new SchoolPlanInfo(
            Code,
            LabelEs,
            PriceCop,
            monthly,
            DurationMonths,
            MaxTeachers ?? 0,
            MaxStudents ?? 0);
    }
}

public static class PaymentAdminStatuses
{
    public const string None = "None";
    public const string Pending = "Pending";
    public const string Reported = "Reported";
    public const string Confirmed = "Confirmed";
    public const string Rejected = "Rejected";

    public static bool IsValid(string? value) => value is
        None or Pending or Reported or Confirmed or Rejected;
}
