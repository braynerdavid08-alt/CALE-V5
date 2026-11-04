using System.Net;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Application.Services;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services;

/// <summary>
/// Public commercial directory + school pages.
/// Uses <see cref="ISchoolCommercialPresenceGate"/> only —
/// does not reuse the looser <c>/api/public/schools</c> listing rules.
/// </summary>
public sealed class CommercialDirectoryService
{
    private readonly CaleDbContext _db;
    private readonly IClock _clock;
    private readonly ISchoolCommercialPresenceGate _gate;

    public CommercialDirectoryService(
        CaleDbContext db,
        IClock clock,
        ISchoolCommercialPresenceGate gate)
    {
        _db = db;
        _clock = clock;
        _gate = gate;
    }

    public async Task<IReadOnlyList<CommercialSchoolCardDto>> ListAsync(
        int take,
        CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var limit = Math.Clamp(take, 1, 50);

        var commercialRows = await _db.Set<SchoolCommercialProfile>().AsNoTracking()
            .Where(c => c.PublicationStatus == SchoolCommercialPublicationStatus.Published
                && c.CommerciallySuspendedAt == null)
            .ToListAsync(ct);

        if (commercialRows.Count == 0)
        {
            return [];
        }

        var schoolIds = commercialRows.Select(c => c.SchoolUserId).ToList();
        var schools = await _db.Set<SchoolProfile>().AsNoTracking()
            .Where(s => schoolIds.Contains(s.UserId))
            .ToListAsync(ct);
        var schoolById = schools.ToDictionary(s => s.UserId);

        var cards = new List<CommercialSchoolCardDto>(commercialRows.Count);
        foreach (var commercial in commercialRows)
        {
            if (!schoolById.TryGetValue(commercial.SchoolUserId, out var school))
            {
                continue;
            }

            var decision = _gate.Evaluate(school, commercial, now);
            if (!decision.Eligible)
            {
                continue;
            }

            cards.Add(ToCard(school, commercial));
        }

        return cards
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    public async Task<CommercialSchoolDetailDto> GetBySlugAsync(
        string slug,
        CancellationToken ct = default)
    {
        var normalized = NormalizePublicSlug(slug);
        var commercial = await _db.Set<SchoolCommercialProfile>().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == normalized, ct)
            ?? throw new NotFoundException("Escuela no encontrada.", "commercial_school_not_found");

        var school = await _db.Set<SchoolProfile>().AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == commercial.SchoolUserId, ct)
            ?? throw new NotFoundException("Escuela no encontrada.", "commercial_school_not_found");

        var now = _clock.UtcNow;
        var decision = _gate.Evaluate(school, commercial, now);
        if (!decision.Eligible)
        {
            // Hide drafts / expired / FreeAccess-only schools from the public web.
            throw new NotFoundException("Escuela no encontrada.", "commercial_school_not_found");
        }

        var today = DateOnly.FromDateTime(now);
        var posts = await _db.Set<SchoolCommercialPost>().AsNoTracking()
            .Where(p => p.SchoolUserId == commercial.SchoolUserId
                && p.Status == SchoolCommercialContentStatuses.Published)
            .OrderByDescending(p => p.PublishedAt ?? p.CreatedAt)
            .Take(20)
            .ToListAsync(ct);
        var promos = await _db.Set<SchoolCommercialPromotion>().AsNoTracking()
            .Where(p => p.SchoolUserId == commercial.SchoolUserId
                && p.Status == SchoolCommercialContentStatuses.Published
                && p.StartsOn <= today
                && p.EndsOn >= today)
            .OrderBy(p => p.EndsOn)
            .Take(10)
            .ToListAsync(ct);

        return ToDetail(school, commercial, posts, promos);
    }

    private static CommercialSchoolCardDto ToCard(
        SchoolProfile school,
        SchoolCommercialProfile commercial)
    {
        var city = PublicPart(school.City);
        var department = PublicPart(school.Department);
        var slug = commercial.Slug;
        var detailPath = string.IsNullOrWhiteSpace(slug)
            ? "/escuelas"
            : $"/escuelas/{slug}";

        return new CommercialSchoolCardDto(
            school.UserId,
            school.LegalName.Trim(),
            city,
            department,
            commercial.About.Trim(),
            commercial.LicenseCategories.Trim(),
            MediaUrl(commercial.CoverMediaId),
            MediaUrl(commercial.LogoMediaId),
            slug,
            detailPath);
    }

    private static CommercialSchoolDetailDto ToDetail(
        SchoolProfile school,
        SchoolCommercialProfile commercial,
        IReadOnlyList<SchoolCommercialPost> posts,
        IReadOnlyList<SchoolCommercialPromotion> promos)
    {
        var city = PublicPart(school.City);
        var department = PublicPart(school.Department);
        var address = PublicPart(school.Address);
        var phone = PublicPart(school.Phone);
        var slug = commercial.Slug!.Trim();
        var whatsApp = commercial.WhatsApp;
        return new CommercialSchoolDetailDto(
            school.UserId,
            school.LegalName.Trim(),
            city,
            department,
            address,
            phone,
            commercial.About.Trim(),
            commercial.LicenseCategories.Trim(),
            MediaUrl(commercial.CoverMediaId),
            MediaUrl(commercial.LogoMediaId),
            slug,
            $"/escuelas/{slug}",
            BuildMapsUrl(address, city, department),
            whatsApp,
            commercial.PublicEmail,
            string.IsNullOrWhiteSpace(whatsApp)
                ? null
                : $"https://wa.me/{whatsApp}",
            posts.Select(p => new CommercialSchoolPostDto(
                p.Id,
                p.Body,
                MediaUrl(p.MediaId),
                p.PublishedAt)).ToList(),
            promos.Select(p => new CommercialSchoolPromotionDto(
                p.Id,
                p.Title,
                p.Body,
                MediaUrl(p.MediaId),
                p.StartsOn,
                p.EndsOn)).ToList());
    }

    private static string NormalizePublicSlug(string? slug)
    {
        var trimmed = (slug ?? "").Trim().ToLowerInvariant();
        if (trimmed.Length is 0 or > SchoolCommercialProfile.SlugMaxLength)
        {
            throw new NotFoundException("Escuela no encontrada.", "commercial_school_not_found");
        }

        return trimmed;
    }

    private static string? BuildMapsUrl(string address, string city, string department)
    {
        var parts = new[] { address, city, department }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();
        if (parts.Length == 0)
        {
            return null;
        }

        var query = string.Join(", ", parts);
        return "https://www.google.com/maps/search/?api=1&query="
            + WebUtility.UrlEncode(query);
    }

    private static string PublicPart(string value) =>
        value == SchoolProfile.NotRegistered ? "" : value.Trim();

    private static string? MediaUrl(Guid? id) =>
        id is { } mediaId ? $"/api/media/{mediaId:D}" : null;
}
