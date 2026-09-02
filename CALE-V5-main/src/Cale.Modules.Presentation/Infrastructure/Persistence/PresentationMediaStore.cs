using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Presentation.Application.Abstractions;
using Cale.Modules.Presentation.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Cale.Modules.Presentation.Infrastructure.Persistence;

public sealed class PresentationMediaStore : IPresentationMediaStore
{
    private readonly CaleDbContext _db;
    private readonly string? _legacyDiskDir;

    public PresentationMediaStore(
        CaleDbContext db,
        IHostEnvironment env,
        IConfiguration config)
    {
        _db = db;
        var configured = config["Uploads:Root"]
            ?? Environment.GetEnvironmentVariable("UPLOADS_ROOT");
        var uploadsRoot = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "wwwroot", "uploads")
            : configured.Trim();
        _legacyDiskDir = Path.Combine(uploadsRoot, "presentations");
    }

    public string BuildPublicUrl(Guid id) => $"/api/presentations/media/{id:D}";

    public async Task<string> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        int? ownerId,
        CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var data = ms.ToArray();
        var id = Guid.NewGuid();
        var blob = PresentationMediaBlob.Create(
            id,
            fileName,
            contentType,
            data,
            ownerId,
            DateTime.UtcNow);
        await _db.Set<PresentationMediaBlob>().AddAsync(blob, ct);
        await _db.SaveChangesAsync(ct);
        return BuildPublicUrl(id);
    }

    public async Task<(byte[] Data, string ContentType, string FileName)?> ReadAsync(
        Guid id,
        CancellationToken ct = default)
    {
        var blob = await _db.Set<PresentationMediaBlob>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (blob is null)
        {
            return null;
        }

        return (blob.Data, blob.ContentType, blob.FileName);
    }

    public async Task<(byte[] Data, string ContentType, string FileName)?> TryReadLegacyDiskAsync(
        string fileName,
        CancellationToken ct = default)
    {
        var path = ResolveLegacyDiskPath(fileName);
        if (path is null)
        {
            return null;
        }

        var safe = Path.GetFileName(fileName)!;
        var bytes = await File.ReadAllBytesAsync(path, ct);
        return (bytes, GuessContentType(safe), safe);
    }

    private string? ResolveLegacyDiskPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(_legacyDiskDir))
        {
            return null;
        }

        var safe = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safe))
        {
            return null;
        }

        var path = Path.Combine(_legacyDiskDir, safe);
        return File.Exists(path) ? path : null;
    }

    private static string GuessContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            ".m4v" => "video/x-m4v",
            _ => "application/octet-stream"
        };
    }
}
