using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Courses.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cale.Api.Services;

/// <summary>
/// Public showcase of the platform courses: cover, description and lesson titles only.
/// School courses and lesson content stay behind login.
/// </summary>
public sealed class PublicCourseCatalogService
{
    private const string CacheKey = "cale.public.courses.v1";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly CaleDbContext _db;
    private readonly IMemoryCache _cache;

    public PublicCourseCatalogService(CaleDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<IReadOnlyList<PublicCourseDto>> ListAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<PublicCourseDto>? cached) && cached is not null)
        {
            return cached;
        }

        var courses = await _db.Set<Course>().AsNoTracking()
            .Where(c => c.SchoolUserId == null && c.IsPublished && c.IsActive)
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.Title, c.Description, c.Category, c.CoverUrl })
            .ToListAsync(ct);
        var ids = courses.Select(c => c.Id).ToList();
        var lessons = await _db.Set<CourseLesson>().AsNoTracking()
            .Where(l => ids.Contains(l.CourseId))
            .OrderBy(l => l.Position)
            .Select(l => new { l.CourseId, l.Title, l.EstimatedMinutes })
            .ToListAsync(ct);
        var byCourse = lessons.ToLookup(l => l.CourseId);

        IReadOnlyList<PublicCourseDto> result = courses
            .Select(c => new PublicCourseDto(
                c.Id,
                c.Title,
                c.Description,
                c.Category,
                IsPublicImage(c.CoverUrl) ? c.CoverUrl : null,
                byCourse[c.Id].Select(l => l.Title).ToList(),
                byCourse[c.Id].Sum(l => l.EstimatedMinutes)))
            .ToList();

        _cache.Set(CacheKey, result, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl, Size = 1 });
        return result;
    }

    private static bool IsPublicImage(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && url.StartsWith('/')
        && !url.StartsWith("//", StringComparison.Ordinal);
}

public sealed record PublicCourseDto(
    int Id,
    string Title,
    string? Description,
    string Category,
    string? CoverUrl,
    IReadOnlyList<string> Lessons,
    int EstimatedMinutes);
