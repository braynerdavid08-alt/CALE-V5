namespace Cale.Modules.Identity.Domain;

public static class SchoolRegistrationKeys
{
    public static string NormalizeTaxId(string value)
    {
        var chars = value.Where(char.IsLetterOrDigit).ToArray();
        return chars.Length == 0 ? "" : new string(chars).ToUpperInvariant();
    }

    public static string NormalizeEmail(string value) =>
        value.Trim().ToLowerInvariant();

    public static string NormalizePhone(string value)
    {
        var digits = value.Where(char.IsDigit).ToArray();
        return digits.Length == 0 ? "" : new string(digits);
    }

    public static string NormalizeLegalName(string value)
    {
        var parts = value.Trim().ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? "" : string.Join(' ', parts);
    }

    public static string NormalizeCity(string value) =>
        value.Trim().ToLowerInvariant();
}
