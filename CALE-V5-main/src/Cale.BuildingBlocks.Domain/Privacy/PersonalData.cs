namespace Cale.BuildingBlocks.Domain.Privacy;

/// <summary>Helpers to keep personal data out of logs.</summary>
public static class PersonalData
{
    /// <summary>"brayner@gmail.com" → "br***@gmail.com". Enough to tell accounts apart without exposing the address.</summary>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "(vacío)";
        }

        var value = email.Trim();
        var at = value.LastIndexOf('@');
        if (at <= 0)
        {
            return "***";
        }

        var local = value[..at];
        var visible = local.Length <= 2 ? local[..1] : local[..2];
        return $"{visible}***{value[at..]}";
    }
}
