using Cale.Api.Extensions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    private const int MaxDailyUploadsForOthers = 20;

    private readonly ICatalogMediaStore _media;

    public MediaController(ICatalogMediaStore media) => _media = media;

    /// <summary>
    /// Stores question/exam images in the database (survives Render redeploys).
    /// Returns a stable public URL like /api/media/{guid}.
    /// </summary>
    [HttpPost("upload")]
    [Authorize]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile? file,
        [FromServices] CaleDbContext db,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw new DomainException("Selecciona una imagen.", 400, "invalid_file");
        }

        var userId = CurrentUser.GetId(User);
        if (!User.IsInRole(Roles.Admin) && !User.IsInRole(Roles.Teacher))
        {
            // Students/schools upload images only for question proposals: cap per day.
            var since = DateTime.UtcNow.AddHours(-24);
            var used = await db.Set<CatalogMediaBlob>().AsNoTracking()
                .CountAsync(b => b.OwnerId == userId && b.CreatedAt >= since, ct);
            if (used >= MaxDailyUploadsForOthers)
            {
                throw new DomainException(
                    "Alcanzaste el máximo de imágenes por hoy. Intenta mañana.",
                    429,
                    "upload_limit_reached");
            }
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
            userId,
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
