using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;

namespace Cale.Api.Controllers;

/// <summary>
/// Read-only Código Nacional de Tránsito dataset extracted from Transiteca (literal).
/// Available to Student, Teacher, School and Admin.
/// </summary>
[ApiController]
[Route("api/codigo-transito")]
[Authorize]
public sealed class CodigoTransitoController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CodigoTransitoController> _logger;
    private CodigoDataset? _cache;

    public CodigoTransitoController(IWebHostEnvironment env, ILogger<CodigoTransitoController> logger)
    {
        _env = env;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<object> GetIndex([FromQuery] string? q = null)
    {
        var data = Load();
        if (data is null)
        {
            return NotFound(new { error = "codigo_dataset_missing", message = "Dataset de normas no disponible." });
        }

        IEnumerable<CodigoArticleSummary> articles = data.Articles.Select(a => new CodigoArticleSummary(
            a.Number,
            a.Name,
            a.SourceUrl,
            Truncate(a.PlainText, 200),
            a.Notes.Select(n => n.Variant).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToArray(),
            a.ContentHash));

        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.Trim();
            articles = data.Articles
                .Where(a =>
                    a.Number.ToString().Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || (a.Name?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (a.PlainText?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false))
                .Select(a => new CodigoArticleSummary(
                    a.Number,
                    a.Name,
                    a.SourceUrl,
                    Truncate(a.PlainText, 200),
                    a.Notes.Select(n => n.Variant).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToArray(),
                    a.ContentHash));
        }

        return Ok(new
        {
            meta = data.Meta,
            articles = articles.OrderBy(a => a.Number).ToList()
        });
    }

    [HttpGet("{number:int}")]
    public ActionResult<object> GetArticle(int number)
    {
        var data = Load();
        if (data is null)
        {
            return NotFound(new { error = "codigo_dataset_missing" });
        }

        var article = data.Articles.FirstOrDefault(a => a.Number == number);
        if (article is null)
        {
            return NotFound(new { error = "article_not_found", number });
        }

        var prev = data.Articles.Where(a => a.Number < number).OrderByDescending(a => a.Number).FirstOrDefault();
        var next = data.Articles.Where(a => a.Number > number).OrderBy(a => a.Number).FirstOrDefault();

        return Ok(new
        {
            meta = data.Meta,
            article,
            prev = prev is null ? null : new { number = prev.Number, name = prev.Name },
            next = next is null ? null : new { number = next.Number, name = next.Name }
        });
    }

    private CodigoDataset? Load()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        var path = ResolveDatasetPath();
        if (path is null)
        {
            _logger.LogWarning("Codigo transito dataset not found under SeedData/codigo-transito.");
            return null;
        }

        try
        {
            var json = System.IO.File.ReadAllText(path);
            _cache = JsonSerializer.Deserialize<CodigoDataset>(json, JsonOpts);
            return _cache;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load codigo transito dataset from {Path}", path);
            return null;
        }
    }

    private string? ResolveDatasetPath()
    {
        var candidates = new[]
        {
            Path.Combine(_env.ContentRootPath, "SeedData", "codigo-transito", "articles.json"),
            Path.Combine(AppContext.BaseDirectory, "SeedData", "codigo-transito", "articles.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "SeedData", "codigo-transito", "articles.json")
        };
        return candidates.FirstOrDefault(System.IO.File.Exists);
    }

    private static string Truncate(string? text, int max)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var t = text.Trim();
        return t.Length <= max ? t : t[..max].TrimEnd() + "…";
    }

    private sealed record CodigoDataset(CodigoMeta Meta, List<CodigoArticle> Articles);

    private sealed record CodigoMeta(
        string? Title,
        string? SourceBase,
        string? LawHint,
        string? ExtractedAt,
        string? ExtractorVersion,
        int? ArticleCount,
        string? Attribution,
        string? PublishedAt,
        string? Note);

    private sealed record CodigoArticle(
        int Number,
        string? Name,
        string? SourceUrl,
        string? ContentSource,
        JsonElement Blocks,
        string PlainText,
        List<CodigoNote> Notes,
        List<string> Flags,
        string? ContentHash,
        string? FetchedAt);

    private sealed record CodigoNote(string? Variant, string? Text);

    private sealed record CodigoArticleSummary(
        int Number,
        string? Name,
        string? SourceUrl,
        string? Preview,
        string?[] Notes,
        string? ContentHash);
}
