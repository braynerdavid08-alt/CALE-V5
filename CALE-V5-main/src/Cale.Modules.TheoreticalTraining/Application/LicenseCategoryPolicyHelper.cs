using System.Text.Json;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;

namespace Cale.Modules.TheoreticalTraining.Application;

internal sealed class LicenseCategoryPolicyStore
{
    public int? RequiredTheoryHours { get; set; }
    public int? RequiredWorkshopHours { get; set; }
}

public static class LicenseCategoryPolicyHelper
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IReadOnlyList<LicenseCategoryPolicyDto> BuildPolicyList(TheoryTrainingSettings settings)
    {
        var overrides = Deserialize(settings.LicenseCategoryPoliciesJson);
        return StudentLicenseCategories.Presets
            .Select(code => new LicenseCategoryPolicyDto(
                code,
                StudentLicenseCategories.FormatLabel(code),
                overrides.TryGetValue(code, out var stored) ? stored.RequiredTheoryHours : null,
                overrides.TryGetValue(code, out stored) ? stored.RequiredWorkshopHours : null))
            .ToList();
    }

    public static string SerializePolicies(IReadOnlyList<LicenseCategoryPolicyDto>? policies)
    {
        if (policies is null || policies.Count == 0)
        {
            return "{}";
        }

        var dict = new Dictionary<string, LicenseCategoryPolicyStore>(StringComparer.OrdinalIgnoreCase);
        foreach (var policy in policies)
        {
            if (string.IsNullOrWhiteSpace(policy.Code))
            {
                continue;
            }

            if (policy.RequiredTheoryHours is null && policy.RequiredWorkshopHours is null)
            {
                continue;
            }

            dict[policy.Code.Trim().ToUpperInvariant()] = new LicenseCategoryPolicyStore
            {
                RequiredTheoryHours = policy.RequiredTheoryHours,
                RequiredWorkshopHours = policy.RequiredWorkshopHours
            };
        }

        return JsonSerializer.Serialize(dict, JsonOpts);
    }

    public static (int TheoryHours, int WorkshopHours) ResolveHourRequirements(
        TheoryTrainingSettings settings,
        string? licenseCategories)
    {
        var code = NormalizeLicenseCode(licenseCategories);
        var overrides = Deserialize(settings.LicenseCategoryPoliciesJson);
        if (code is not null
            && overrides.TryGetValue(code, out var stored))
        {
            return (
                stored.RequiredTheoryHours ?? settings.RequiredTheoryHours,
                stored.RequiredWorkshopHours ?? settings.RequiredWorkshopHours);
        }

        return (settings.RequiredTheoryHours, settings.RequiredWorkshopHours);
    }

    private static string? NormalizeLicenseCode(string? licenseCategories)
    {
        if (string.IsNullOrWhiteSpace(licenseCategories))
        {
            return null;
        }

        var trimmed = licenseCategories.Trim();
        return StudentLicenseCategories.Presets
            .FirstOrDefault(p => string.Equals(p, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, LicenseCategoryPolicyStore> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
        {
            return new Dictionary<string, LicenseCategoryPolicyStore>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, LicenseCategoryPolicyStore>>(json, JsonOpts)
                ?? new Dictionary<string, LicenseCategoryPolicyStore>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, LicenseCategoryPolicyStore>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
