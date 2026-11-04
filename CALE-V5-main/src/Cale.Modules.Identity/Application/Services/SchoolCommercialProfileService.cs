using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.Identity.Application.Services;

public interface ISchoolCommercialProfileService
{
    Task<SchoolCommercialProfileDto> GetOwnAsync(int actorUserId, CancellationToken ct = default);

    Task<SchoolCommercialProfileDto> UpdateOwnAsync(
        int actorUserId,
        UpdateSchoolCommercialProfileRequest request,
        CancellationToken ct = default);

    Task<SchoolCommercialProfileDto> SetPublicationAsync(
        int actorUserId,
        bool publish,
        CancellationToken ct = default);

    Task SuspendByAdminAsync(int schoolUserId, string reason, CancellationToken ct = default);

    Task ClearSuspensionByAdminAsync(int schoolUserId, CancellationToken ct = default);

    Task<AdminCommercialPresenceDto> GetAdminViewAsync(int schoolUserId, CancellationToken ct = default);
}

public sealed class SchoolCommercialProfileService : ISchoolCommercialProfileService
{
    private readonly CaleDbContext _db;
    private readonly ISchoolProfileStore _profiles;
    private readonly IClock _clock;
    private readonly ISchoolCommercialPresenceGate _gate;
    private readonly ISchoolCommercialAccessPolicy _access;

    public SchoolCommercialProfileService(
        CaleDbContext db,
        ISchoolProfileStore profiles,
        IClock clock,
        ISchoolCommercialPresenceGate gate,
        ISchoolCommercialAccessPolicy access)
    {
        _db = db;
        _profiles = profiles;
        _clock = clock;
        _gate = gate;
        _access = access;
    }

    public async Task<SchoolCommercialProfileDto> GetOwnAsync(int actorUserId, CancellationToken ct = default)
    {
        await EnsureCanManageAsync(actorUserId, actorUserId, ct);
        var school = await RequireSchoolAsync(actorUserId, ct);
        var commercial = await _db.Set<SchoolCommercialProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SchoolUserId == actorUserId, ct);
        var stats = await LoadStatsAsync(actorUserId, ct);
        return Map(school, commercial, stats);
    }

    public async Task<SchoolCommercialProfileDto> UpdateOwnAsync(
        int actorUserId,
        UpdateSchoolCommercialProfileRequest request,
        CancellationToken ct = default)
    {
        await EnsureCanManageAsync(actorUserId, actorUserId, ct);
        var school = await RequireSchoolAsync(actorUserId, ct);
        var now = _clock.UtcNow;
        var commercial = await _db.Set<SchoolCommercialProfile>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == actorUserId, ct);
        if (commercial is null)
        {
            commercial = SchoolCommercialProfile.Create(actorUserId, now);
            commercial.SetSlug(
                string.IsNullOrWhiteSpace(request.Slug)
                    ? BuildSlugCandidate(school.LegalName, actorUserId)
                    : request.Slug!,
                now);
            _db.Add(commercial);
        }

        if (commercial.IsCommerciallySuspended)
        {
            throw new DomainException(
                "La presencia comercial está suspendida por Luz Verde.",
                403,
                "commercial_suspended");
        }

        commercial.UpdateSheet(
            request.About,
            request.LicenseCategories,
            request.CoverMediaId,
            request.LogoMediaId,
            now);
        commercial.SetPublicContact(request.WhatsApp, request.PublicEmail, now);

        if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            commercial.SetSlug(request.Slug, now);
        }
        else if (string.IsNullOrWhiteSpace(commercial.Slug))
        {
            commercial.SetSlug(BuildSlugCandidate(school.LegalName, actorUserId), now);
        }

        await EnsureUniqueSlugAsync(commercial, ct);
        await _db.SaveChangesAsync(ct);
        return Map(school, commercial, await LoadStatsAsync(actorUserId, ct));
    }

    public async Task<SchoolCommercialProfileDto> SetPublicationAsync(
        int actorUserId,
        bool publish,
        CancellationToken ct = default)
    {
        await EnsureCanManageAsync(actorUserId, actorUserId, ct);
        var school = await RequireSchoolAsync(actorUserId, ct);
        var commercial = await _db.Set<SchoolCommercialProfile>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == actorUserId, ct)
            ?? throw new DomainException(
                "Configura la ficha comercial antes de publicar.",
                400,
                "commercial_profile_missing");

        var now = _clock.UtcNow;
        if (publish)
        {
            var decision = _gate.Evaluate(school, commercial, now);
            if (!decision.Eligible
                && decision.DenyReason is not SchoolCommercialPresenceDenyReasons.NotPublished)
            {
                throw new DomainException(
                    "La escuela no cumple los requisitos comerciales para publicarse.",
                    403,
                    decision.DenyReason ?? "commercial_not_eligible");
            }

            commercial.Publish(now);
        }
        else
        {
            commercial.Unpublish(now);
        }

        await _db.SaveChangesAsync(ct);
        return Map(school, commercial, await LoadStatsAsync(actorUserId, ct));
    }

    public async Task SuspendByAdminAsync(int schoolUserId, string reason, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        var commercial = await _db.Set<SchoolCommercialProfile>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId, ct);
        if (commercial is null)
        {
            commercial = SchoolCommercialProfile.Create(schoolUserId, now);
            commercial.SetSlug(BuildSlugCandidate($"escuela-{schoolUserId}", schoolUserId), now);
            _db.Add(commercial);
        }

        commercial.SuspendCommercial(reason, now);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ClearSuspensionByAdminAsync(int schoolUserId, CancellationToken ct = default)
    {
        var commercial = await _db.Set<SchoolCommercialProfile>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId, ct)
            ?? throw new DomainException("Ficha comercial no encontrada.", 404, "commercial_profile_missing");
        commercial.ClearCommercialSuspension(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<AdminCommercialPresenceDto> GetAdminViewAsync(int schoolUserId, CancellationToken ct = default)
    {
        var school = await RequireSchoolAsync(schoolUserId, ct);
        var commercial = await _db.Set<SchoolCommercialProfile>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId, ct);
        var decision = _gate.Evaluate(school, commercial, _clock.UtcNow);
        return new AdminCommercialPresenceDto(
            school.UserId,
            school.LegalName,
            commercial?.Slug,
            commercial?.PublicationStatus ?? SchoolCommercialPublicationStatus.Draft,
            commercial?.IsPublished ?? false,
            commercial?.IsCommerciallySuspended ?? false,
            commercial?.CommercialSuspensionReason,
            decision.Eligible,
            decision.DenyReason);
    }

    private async Task<SchoolCommercialStatsDto> LoadStatsAsync(int schoolUserId, CancellationToken ct)
    {
        var since = _clock.UtcNow.AddDays(-30);
        var rows = await _db.Set<SchoolCommercialEvent>().AsNoTracking()
            .Where(e => e.SchoolUserId == schoolUserId && e.CreatedAt >= since)
            .GroupBy(e => e.EventType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Count(string type) => rows.FirstOrDefault(r => r.Type == type)?.Count ?? 0;
        return new SchoolCommercialStatsDto(
            Count(SchoolCommercialEventTypes.PageView),
            Count(SchoolCommercialEventTypes.WhatsAppClick),
            Count(SchoolCommercialEventTypes.MapsClick),
            Count(SchoolCommercialEventTypes.PhoneClick));
    }

    private async Task EnsureCanManageAsync(int actorUserId, int schoolUserId, CancellationToken ct)
    {
        if (!await _access.CanManageAsync(actorUserId, schoolUserId, ct))
        {
            throw new DomainException(
                "No puedes administrar la ficha comercial de otra escuela.",
                403,
                "commercial_forbidden");
        }
    }

    private async Task<SchoolProfile> RequireSchoolAsync(int schoolUserId, CancellationToken ct)
    {
        var school = await _profiles.GetByUserIdAsync(schoolUserId, ct);
        if (school is null)
        {
            throw new DomainException("Escuela no encontrada.", 404, "school_not_found");
        }

        return school;
    }

    private async Task EnsureUniqueSlugAsync(SchoolCommercialProfile commercial, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(commercial.Slug))
        {
            return;
        }

        var taken = await _db.Set<SchoolCommercialProfile>().AsNoTracking()
            .AnyAsync(
                x => x.Slug == commercial.Slug && x.SchoolUserId != commercial.SchoolUserId,
                ct);
        if (!taken)
        {
            return;
        }

        commercial.SetSlug($"{commercial.Slug}-{commercial.SchoolUserId}", _clock.UtcNow);
    }

    private SchoolCommercialProfileDto Map(
        SchoolProfile school,
        SchoolCommercialProfile? commercial,
        SchoolCommercialStatsDto? stats)
    {
        var now = _clock.UtcNow;
        var decision = _gate.Evaluate(school, commercial, now);
        var slug = commercial?.Slug;
        return new SchoolCommercialProfileDto(
            school.UserId,
            commercial?.PublicationStatus ?? SchoolCommercialPublicationStatus.Draft,
            commercial?.About ?? "",
            commercial?.LicenseCategories ?? "",
            commercial?.CoverMediaId,
            commercial?.LogoMediaId,
            MediaUrl(commercial?.CoverMediaId),
            MediaUrl(commercial?.LogoMediaId),
            slug,
            commercial?.WhatsApp,
            commercial?.PublicEmail,
            commercial?.IsSheetComplete ?? false,
            commercial?.IsPublished ?? false,
            commercial?.IsCommerciallySuspended ?? false,
            commercial?.CommercialSuspensionReason,
            decision.Eligible,
            decision.DenyReason,
            commercial?.PublishedAt,
            commercial?.UpdatedAt ?? school.CreatedAt,
            string.IsNullOrWhiteSpace(slug) ? null : $"/escuelas/{slug}",
            stats);
    }

    private static string? MediaUrl(Guid? id) =>
        id is { } mediaId ? $"/api/media/{mediaId:D}" : null;

    private static string BuildSlugCandidate(string legalName, int schoolUserId)
    {
        try
        {
            return SchoolCommercialProfile.NormalizeSlug(legalName);
        }
        catch (DomainException)
        {
            return SchoolCommercialProfile.NormalizeSlug($"escuela-{schoolUserId}");
        }
    }
}
