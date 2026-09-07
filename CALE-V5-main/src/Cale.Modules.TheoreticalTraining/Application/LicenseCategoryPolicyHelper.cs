using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;

namespace Cale.Modules.TheoreticalTraining.Application;

/// <summary>
/// Hour requirements are platform constants (TheoryHourStandards), not school-editable.
/// </summary>
public static class LicenseCategoryPolicyHelper
{
    public static IReadOnlyList<LicenseCategoryPolicyDto> BuildPolicyList(TheoryTrainingSettings? settings = null)
    {
        _ = settings; // retained for call-site compatibility
        return StudentLicenseCategories.Presets
            .Select(code =>
            {
                var (theory, workshop) = TheoryHourStandards.ForLicense(code);
                return new LicenseCategoryPolicyDto(
                    code,
                    StudentLicenseCategories.FormatLabel(code),
                    theory,
                    workshop);
            })
            .ToList();
    }

    /// <summary>Ignored — hours are fixed constants. Kept so callers compile.</summary>
    public static string SerializePolicies(IReadOnlyList<LicenseCategoryPolicyDto>? policies)
    {
        _ = policies;
        return "{}";
    }

    public static (int TheoryHours, int WorkshopHours) ResolveHourRequirements(
        TheoryTrainingSettings settings,
        string? licenseCategories)
    {
        _ = settings;
        return TheoryHourStandards.ForLicense(licenseCategories);
    }
}
