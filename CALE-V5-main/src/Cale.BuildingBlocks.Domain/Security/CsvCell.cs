namespace Cale.BuildingBlocks.Domain.Security;

/// <summary>
/// Quotes CSV cells and neutralises spreadsheet formulas (CSV injection):
/// values starting with = + - @ tab or CR get a leading apostrophe.
/// </summary>
public static class CsvCell
{
    public static string Escape(string? value)
    {
        var v = value ?? "";
        if (v.Length > 0 && v[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            v = "'" + v;
        }

        return $"\"{v.Replace("\"", "\"\"")}\"";
    }

    public static string Unescape(string value) =>
        value.Length > 1 && value[0] == '\'' && value[1] is '=' or '+' or '-' or '@' or '\t' or '\r'
            ? value[1..]
            : value;
}
