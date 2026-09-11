using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.Catalog.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Cale.Modules.Catalog.Infrastructure.Persistence;

public sealed class CatalogMediaStore : ICatalogMediaStore
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(2);
    private readonly CaleDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly string _uploadsRoot;

    public CatalogMediaStore(
        CaleDbContext db,
        IMemoryCache cache,
        IHostEnvironment env,
        IConfiguration config)
    {
        _db = db;
        _cache = cache;
        var configured = config["Uploads:Root"]
            ?? Environment.GetEnvironmentVariable("UPLOADS_ROOT");
        _uploadsRoot = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "wwwroot", "uploads")
            : configured.Trim();
    }

    public string BuildPublicUrl(Guid id) => $"/api/media/{id:D}";

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
        var blob = CatalogMediaBlob.Create(
            id,
            fileName,
            NormalizeContentType(contentType, fileName),
            data,
            ownerId,
            DateTime.UtcNow);
        await _db.Set<CatalogMediaBlob>().AddAsync(blob, ct);
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

        var blob = await _db.Set<CatalogMediaBlob>()
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
        var safe = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safe))
        {
            return null;
        }

        var path = Path.Combine(_uploadsRoot, safe);
        if (!File.Exists(path))
        {
            // Also check nested presentations folder is NOT used for exam media.
            return null;
        }

        var cacheKey = $"catalog-media-legacy:{safe}";
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

    private static string CacheKey(Guid id) => $"catalog-media:{id:D}";

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
            _ => "application/octet-stream"
        };
    }
}
