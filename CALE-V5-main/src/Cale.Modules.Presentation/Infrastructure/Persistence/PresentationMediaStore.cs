using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Presentation.Application.Abstractions;
using Cale.Modules.Presentation.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Cale.Modules.Presentation.Infrastructure.Persistence;

public sealed class PresentationMediaStore : IPresentationMediaStore
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(2);
    private readonly CaleDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly string? _legacyDiskDir;

    public PresentationMediaStore(
        CaleDbContext db,
        IMemoryCache cache,
        IHostEnvironment env,
        IConfiguration config)
    {
        _db = db;
        _cache = cache;
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
            NormalizeContentType(contentType, fileName),
            data,
            ownerId,
            DateTime.UtcNow);
        await _db.Set<PresentationMediaBlob>().AddAsync(blob, ct);
        await _db.SaveChangesAsync(ct);
        _cache.Set(
            CacheKey(id),
            (blob.Data, blob.ContentType, blob.FileName),
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl,
                Size = Math.Max(1, blob.Data.LongLength)
            });
        return BuildPublicUrl(id);
    }

    public async Task<(byte[] Data, string ContentType, string FileName)?> ReadAsync(
        Guid id,
        CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey(id), out (byte[] Data, string ContentType, string FileName) cached))
        {
            return cached;
        }

        var blob = await _db.Set<PresentationMediaBlob>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (blob is null)
        {
            return null;
        }

        var contentType = NormalizeContentType(blob.ContentType, blob.FileName);
        var entry = (blob.Data, contentType, blob.FileName);
        _cache.Set(
            CacheKey(id),
            entry,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl,
                Size = Math.Max(1, blob.Data.LongLength)
            });
        return entry;
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
        var cacheKey = $"pres-media-legacy:{safe}";
        if (_cache.TryGetValue(cacheKey, out (byte[] Data, string ContentType, string FileName) cached))
        {
            return cached;
        }

        var bytes = await File.ReadAllBytesAsync(path, ct);
        var entry = (bytes, GuessContentType(safe), safe);
        _cache.Set(
            cacheKey,
            entry,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl,
                Size = Math.Max(1, bytes.LongLength)
            });
        return entry;
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

    private static string CacheKey(Guid id) => $"pres-media:{id:D}";

    private static string NormalizeContentType(string? contentType, string fileName)
    {
        var ct = (contentType ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(ct)
            && !ct.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return ct;
        }

        return GuessContentType(fileName);
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
            ".avi" => "video/x-msvideo",
            _ => "application/octet-stream"
        };
    }
}
