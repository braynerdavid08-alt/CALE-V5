using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.DTOs;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.Identity.Application.Services;

public interface ISchoolCommercialContentService
{
    Task<IReadOnlyList<SchoolCommercialPostAdminDto>> ListPostsAsync(int schoolUserId, CancellationToken ct = default);
    Task<SchoolCommercialPostAdminDto> UpsertPostAsync(int schoolUserId, int? postId, UpsertSchoolCommercialPostRequest request, CancellationToken ct = default);
    Task<SchoolCommercialPostAdminDto> SetPostStatusAsync(int schoolUserId, int postId, bool publish, CancellationToken ct = default);
    Task DeletePostAsync(int schoolUserId, int postId, CancellationToken ct = default);

    Task<IReadOnlyList<SchoolCommercialPromotionAdminDto>> ListPromotionsAsync(int schoolUserId, CancellationToken ct = default);
    Task<SchoolCommercialPromotionAdminDto> UpsertPromotionAsync(int schoolUserId, int? promoId, UpsertSchoolCommercialPromotionRequest request, CancellationToken ct = default);
    Task<SchoolCommercialPromotionAdminDto> SetPromotionStatusAsync(int schoolUserId, int promoId, bool publish, CancellationToken ct = default);
    Task DeletePromotionAsync(int schoolUserId, int promoId, CancellationToken ct = default);

    Task RecordPublicEventAsync(string slug, string eventType, CancellationToken ct = default);
}

public sealed class SchoolCommercialContentService : ISchoolCommercialContentService
{
    private readonly CaleDbContext _db;
    private readonly IClock _clock;
    private readonly ISchoolCommercialPresenceGate _gate;
    private readonly ISchoolCommercialAccessPolicy _access;

    public SchoolCommercialContentService(
        CaleDbContext db,
        IClock clock,
        ISchoolCommercialPresenceGate gate,
        ISchoolCommercialAccessPolicy access)
    {
        _db = db;
        _clock = clock;
        _gate = gate;
        _access = access;
    }

    public async Task<IReadOnlyList<SchoolCommercialPostAdminDto>> ListPostsAsync(
        int schoolUserId,
        CancellationToken ct = default)
    {
        await EnsureOwnerAsync(schoolUserId, ct);
        var rows = await _db.Set<SchoolCommercialPost>().AsNoTracking()
            .Where(p => p.SchoolUserId == schoolUserId)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync(ct);
        return rows.Select(MapPost).ToList();
    }

    public async Task<SchoolCommercialPostAdminDto> UpsertPostAsync(
        int schoolUserId,
        int? postId,
        UpsertSchoolCommercialPostRequest request,
        CancellationToken ct = default)
    {
        await EnsureOwnerCanEditAsync(schoolUserId, ct);
        var now = _clock.UtcNow;
        SchoolCommercialPost post;
        if (postId is null)
        {
            post = SchoolCommercialPost.Create(schoolUserId, request.Body, request.MediaId, now);
            _db.Add(post);
        }
        else
        {
            post = await RequirePostAsync(schoolUserId, postId.Value, ct);
            post.Update(request.Body, request.MediaId, now);
        }

        await _db.SaveChangesAsync(ct);
        return MapPost(post);
    }

    public async Task<SchoolCommercialPostAdminDto> SetPostStatusAsync(
        int schoolUserId,
        int postId,
        bool publish,
        CancellationToken ct = default)
    {
        await EnsureOwnerCanEditAsync(schoolUserId, ct);
        var post = await RequirePostAsync(schoolUserId, postId, ct);
        if (publish)
        {
            post.Publish(_clock.UtcNow);
        }
        else
        {
            post.Hide(_clock.UtcNow);
        }

        await _db.SaveChangesAsync(ct);
        return MapPost(post);
    }

    public async Task DeletePostAsync(int schoolUserId, int postId, CancellationToken ct = default)
    {
        await EnsureOwnerCanEditAsync(schoolUserId, ct);
        var post = await RequirePostAsync(schoolUserId, postId, ct);
        _db.Remove(post);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SchoolCommercialPromotionAdminDto>> ListPromotionsAsync(
        int schoolUserId,
        CancellationToken ct = default)
    {
        await EnsureOwnerAsync(schoolUserId, ct);
        var rows = await _db.Set<SchoolCommercialPromotion>().AsNoTracking()
            .Where(p => p.SchoolUserId == schoolUserId)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync(ct);
        return rows.Select(MapPromo).ToList();
    }

    public async Task<SchoolCommercialPromotionAdminDto> UpsertPromotionAsync(
        int schoolUserId,
        int? promoId,
        UpsertSchoolCommercialPromotionRequest request,
        CancellationToken ct = default)
    {
        await EnsureOwnerCanEditAsync(schoolUserId, ct);
        var now = _clock.UtcNow;
        SchoolCommercialPromotion promo;
        if (promoId is null)
        {
            promo = SchoolCommercialPromotion.Create(
                schoolUserId,
                request.Title,
                request.Body,
                request.MediaId,
                request.StartsOn,
                request.EndsOn,
                now);
            _db.Add(promo);
        }
        else
        {
            promo = await RequirePromoAsync(schoolUserId, promoId.Value, ct);
            promo.Update(request.Title, request.Body, request.MediaId, request.StartsOn, request.EndsOn, now);
        }

        await _db.SaveChangesAsync(ct);
        return MapPromo(promo);
    }

    public async Task<SchoolCommercialPromotionAdminDto> SetPromotionStatusAsync(
        int schoolUserId,
        int promoId,
        bool publish,
        CancellationToken ct = default)
    {
        await EnsureOwnerCanEditAsync(schoolUserId, ct);
        var promo = await RequirePromoAsync(schoolUserId, promoId, ct);
        if (publish)
        {
            promo.Publish(_clock.UtcNow);
        }
        else
        {
            promo.Hide(_clock.UtcNow);
        }

        await _db.SaveChangesAsync(ct);
        return MapPromo(promo);
    }

    public async Task DeletePromotionAsync(int schoolUserId, int promoId, CancellationToken ct = default)
    {
        await EnsureOwnerCanEditAsync(schoolUserId, ct);
        var promo = await RequirePromoAsync(schoolUserId, promoId, ct);
        _db.Remove(promo);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RecordPublicEventAsync(string slug, string eventType, CancellationToken ct = default)
    {
        var normalized = (slug ?? "").Trim().ToLowerInvariant();
        var commercial = await _db.Set<SchoolCommercialProfile>().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Slug == normalized, ct)
            ?? throw new NotFoundException("Escuela no encontrada.", "commercial_school_not_found");
        var school = await _db.Set<SchoolProfile>().AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == commercial.SchoolUserId, ct)
            ?? throw new NotFoundException("Escuela no encontrada.", "commercial_school_not_found");

        if (!_gate.Evaluate(school, commercial, _clock.UtcNow).Eligible)
        {
            throw new NotFoundException("Escuela no encontrada.", "commercial_school_not_found");
        }

        _db.Add(SchoolCommercialEvent.Create(commercial.SchoolUserId, eventType, _clock.UtcNow));
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureOwnerAsync(int schoolUserId, CancellationToken ct)
    {
        if (!await _access.CanManageAsync(schoolUserId, schoolUserId, ct))
        {
            throw new DomainException("No autorizado.", 403, "commercial_forbidden");
        }
    }

    private async Task EnsureOwnerCanEditAsync(int schoolUserId, CancellationToken ct)
    {
        await EnsureOwnerAsync(schoolUserId, ct);
        var commercial = await _db.Set<SchoolCommercialProfile>().AsNoTracking()
            .FirstOrDefaultAsync(c => c.SchoolUserId == schoolUserId, ct);
        if (commercial?.IsCommerciallySuspended == true)
        {
            throw new DomainException(
                "La presencia comercial está suspendida por Luz Verde.",
                403,
                "commercial_suspended");
        }
    }

    private async Task<SchoolCommercialPost> RequirePostAsync(int schoolUserId, int postId, CancellationToken ct)
    {
        var post = await _db.Set<SchoolCommercialPost>()
            .FirstOrDefaultAsync(p => p.Id == postId && p.SchoolUserId == schoolUserId, ct);
        return post ?? throw new NotFoundException("Publicación no encontrada.", "commercial_post_not_found");
    }

    private async Task<SchoolCommercialPromotion> RequirePromoAsync(int schoolUserId, int promoId, CancellationToken ct)
    {
        var promo = await _db.Set<SchoolCommercialPromotion>()
            .FirstOrDefaultAsync(p => p.Id == promoId && p.SchoolUserId == schoolUserId, ct);
        return promo ?? throw new NotFoundException("Promoción no encontrada.", "commercial_promotion_not_found");
    }

    private static SchoolCommercialPostAdminDto MapPost(SchoolCommercialPost post) =>
        new(
            post.Id,
            post.Body,
            post.MediaId,
            MediaUrl(post.MediaId),
            post.Status,
            post.PublishedAt,
            post.UpdatedAt);

    private static SchoolCommercialPromotionAdminDto MapPromo(SchoolCommercialPromotion promo) =>
        new(
            promo.Id,
            promo.Title,
            promo.Body,
            promo.MediaId,
            MediaUrl(promo.MediaId),
            promo.StartsOn,
            promo.EndsOn,
            promo.Status,
            promo.UpdatedAt);

    private static string? MediaUrl(Guid? id) =>
        id is { } mediaId ? $"/api/media/{mediaId:D}" : null;
}
