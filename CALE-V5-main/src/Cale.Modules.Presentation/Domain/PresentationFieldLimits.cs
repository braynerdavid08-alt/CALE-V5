namespace Cale.Modules.Presentation.Domain;

internal static class PresentationFieldLimits
{
    public const int TitleMax = 200;
    public const int NotesMax = 4000;
    public const int DescriptionMax = 1000;

    public static string Title(string? value, string fallback) =>
        Truncate(string.IsNullOrWhiteSpace(value) ? fallback : value.Trim(), TitleMax);

    public static string? Notes(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Truncate(value.Trim(), NotesMax);

    public static string? Description(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Truncate(value.Trim(), DescriptionMax);

    public static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
