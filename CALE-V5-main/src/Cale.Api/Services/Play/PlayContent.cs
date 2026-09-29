using System.Globalization;
using System.Text.Json;

namespace Cale.Api.Services.Play;

/// <summary>Static content for the games (traffic signs catalog), loaded once.</summary>
public sealed class PlayContent
{
    private readonly Lazy<IReadOnlyList<SignDto>> _signs;

    public PlayContent(IWebHostEnvironment env, ILogger<PlayContent> logger)
    {
        _signs = new Lazy<IReadOnlyList<SignDto>>(() => LoadSigns(env, logger));
    }

    public IReadOnlyList<SignDto> Signs => _signs.Value;

    private static IReadOnlyList<SignDto> LoadSigns(IWebHostEnvironment env, ILogger logger)
    {
        var path = new[]
            {
                Path.Combine(env.ContentRootPath, "SeedData", "senales-catalog.json"),
                Path.Combine(AppContext.BaseDirectory, "SeedData", "senales-catalog.json")
            }
            .FirstOrDefault(File.Exists);
        if (path is null)
        {
            logger.LogWarning("senales-catalog.json not found; Señal relámpago has no content.");
            return [];
        }

        try
        {
            var raw = JsonSerializer.Deserialize<List<RawSign>>(
                File.ReadAllText(path),
                new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
            return raw
                .Where(s => !string.IsNullOrWhiteSpace(s.Name) && !string.IsNullOrWhiteSpace(s.ImageUrl))
                .Select(s => new SignDto(s.Code ?? "", s.Family ?? "", SentenceCase(s.Name!), s.ImageUrl!))
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read senales-catalog.json.");
            return [];
        }
    }

    private static string SentenceCase(string value)
    {
        var culture = CultureInfo.GetCultureInfo("es-CO");
        var lower = value.Trim().ToLower(culture);
        return lower.Length == 0 ? lower : char.ToUpper(lower[0], culture) + lower[1..];
    }

    private sealed record RawSign(string? Code, string? Family, string? Name, string? ImageUrl);
}
