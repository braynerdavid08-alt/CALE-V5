using Cale.Api.Extensions;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Catalog.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

[ApiController]
[Route("api/media")]
[RequestSizeLimit(6_000_000)]
public sealed class MediaController : ControllerBase
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
    };

    private readonly ICatalogMediaStore _media;

    public MediaController(ICatalogMediaStore media) => _media = media;

    /// <summary>
    /// Stores question/exam images in the database (survives Render redeploys).
    /// Returns a stable public URL like /api/media/{guid}.
    /// </summary>
    [HttpPost("upload")]
    [Authorize(Policy = "TeacherOrAdmin")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw new DomainException("Selecciona una imagen.", 400, "invalid_file");
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            throw new DomainException("La imagen debe pesar 5 MB o menos.", 400, "file_too_large");
        }

        var ext = Path.GetExtension(file.FileName);
        if (!Allowed.Contains(ext))
        {
            throw new DomainException("Usa jpg, png, gif o webp.", 400, "invalid_file");
        }

        var safeExt = ext.ToLowerInvariant();
        await using var stream = file.OpenReadStream();
        var url = await _media.SaveAsync(
            stream,
            $"{Guid.NewGuid():N}{safeExt}",
            file.ContentType,
            CurrentUser.GetId(User),
            ct);
        return Ok(new { url });
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any, NoStore = false)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var blob = await _media.ReadAsync(id, ct);
        if (blob is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "public,max-age=86400,immutable";
        return File(blob.Value.Data, blob.Value.ContentType);
    }

    /// <summary>Fallback for old /uploads/{file} paths still on disk.</summary>
    [HttpGet("legacy/{fileName}")]
    [AllowAnonymous]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any, NoStore = false)]
    public async Task<IActionResult> GetLegacy(string fileName, CancellationToken ct)
    {
        var blob = await _media.TryReadLegacyDiskAsync(fileName, ct);
        if (blob is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "public,max-age=86400,immutable";
        return File(blob.Value.Data, blob.Value.ContentType);
    }
}
