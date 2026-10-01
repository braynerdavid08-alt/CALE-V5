using Cale.Api.Extensions;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Identity.Application.Commands;
using Cale.Modules.Identity.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cale.Api.Controllers;

/// <summary>Every signed-in user can set their own profile photo (stored in the DB like other media).</summary>
[ApiController]
[Authorize]
[Route("api/auth/me/photo")]
public sealed class ProfilePhotoController : ControllerBase
{
    private const long MaxBytes = 3 * 1024 * 1024;

    private static readonly Dictionary<string, string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    private readonly ICatalogMediaStore _media;
    private readonly UpdateMyProfileHandler _profile;

    public ProfilePhotoController(ICatalogMediaStore media, UpdateMyProfileHandler profile)
    {
        _media = media;
        _profile = profile;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(4_000_000)]
    public async Task<ActionResult<MeResponse>> Upload([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw new DomainException("Toma o elige una foto.", 400, "invalid_file");
        }

        if (file.Length > MaxBytes)
        {
            throw new DomainException("La foto debe pesar 3 MB o menos.", 400, "file_too_large");
        }

        if (!Allowed.TryGetValue(file.ContentType ?? "", out var ext))
        {
            throw new DomainException("Usa una foto JPG, PNG o WEBP.", 400, "invalid_file");
        }

        var userId = CurrentUser.GetId(User);
        await using var stream = file.OpenReadStream();
        var url = await _media.SaveAsync(stream, $"perfil-{userId}{ext}", file.ContentType!, userId, ct);

        var (me, previous) = await _profile.SetPhotoAsync(userId, url, ct);
        await DeletePreviousAsync(previous, userId, ct);
        return Ok(me);
    }

    [HttpDelete]
    public async Task<ActionResult<MeResponse>> Remove(CancellationToken ct)
    {
        var userId = CurrentUser.GetId(User);
        var (me, previous) = await _profile.SetPhotoAsync(userId, null, ct);
        await DeletePreviousAsync(previous, userId, ct);
        return Ok(me);
    }

    private async Task DeletePreviousAsync(string? previousUrl, int userId, CancellationToken ct)
    {
        const string prefix = "/api/media/";
        if (previousUrl is null
            || !previousUrl.StartsWith(prefix, StringComparison.Ordinal)
            || !Guid.TryParse(previousUrl[prefix.Length..], out var id))
        {
            return;
        }

        await _media.DeleteOwnedAsync([id], userId, ct);
    }
}
