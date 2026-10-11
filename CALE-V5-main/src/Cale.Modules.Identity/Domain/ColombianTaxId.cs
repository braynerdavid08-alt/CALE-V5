using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Identity.Domain;

/// <summary>
/// NIT colombiano con dígito de verificación (algoritmo DIAN, módulo 11).
/// Forma canónica: "900123456-7" (sin puntos ni espacios).
/// </summary>
public static class ColombianTaxId
{
    private static readonly int[] Weights = [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71];

    public const string InvalidMessage =
        "Escribe un NIT válido con su dígito de verificación, por ejemplo 900123456-7.";

    public static bool TryNormalize(string? raw, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var compact = new string(raw.Where(c => !char.IsWhiteSpace(c) && c != '.' && c != ',').ToArray());
        var dash = compact.IndexOf('-');
        if (dash <= 0 || dash != compact.LastIndexOf('-') || dash != compact.Length - 2)
        {
            return false;
        }

        var body = compact[..dash];
        var check = compact[^1];
        if (body.Length is < 6 or > 15 || !body.All(char.IsAsciiDigit) || !char.IsAsciiDigit(check))
        {
            return false;
        }

        if (body.Distinct().Count() == 1)
        {
            return false;
        }

        if (CheckDigit(body) != check - '0')
        {
            return false;
        }

        normalized = $"{body}-{check}";
        return true;
    }

    public static string Normalize(string? raw) =>
        TryNormalize(raw, out var normalized)
            ? normalized
            : throw new DomainException(InvalidMessage, 400, "invalid_tax_id");

    /// <summary>Comparison key tolerant to legacy formatting ("900.123.456-7", "9001234567").</summary>
    public static string ComparisonKey(string? value) =>
        new string((value ?? "").Where(char.IsAsciiDigit).ToArray());

    public static int CheckDigit(string body)
    {
        var sum = 0;
        for (var i = 0; i < body.Length; i++)
        {
            sum += (body[body.Length - 1 - i] - '0') * Weights[i];
        }

        var remainder = sum % 11;
        return remainder > 1 ? 11 - remainder : remainder;
    }
}
