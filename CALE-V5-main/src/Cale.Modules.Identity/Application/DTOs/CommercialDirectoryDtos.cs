namespace Cale.Modules.Identity.Application.DTOs;

public sealed record CommercialSchoolCardDto(
    int Id,
    string Name,
    string City,
    string Department,
    string About,
    string LicenseCategories,
    string? CoverUrl,
    string? LogoUrl,
    string? Slug,
    string DetailPath);

public sealed record CommercialSchoolPostDto(
    int Id,
    string Body,
    string? MediaUrl,
    DateTime? PublishedAt);

public sealed record CommercialSchoolPromotionDto(
    int Id,
    string Title,
    string Body,
    string? MediaUrl,
    DateOnly StartsOn,
    DateOnly EndsOn);

/// <summary>Public commercial school page (GET /api/public/commercial-schools/{slug}).</summary>
public sealed record CommercialSchoolDetailDto(
    int Id,
    string Name,
    string City,
    string Department,
    string Address,
    string Phone,
    string About,
    string LicenseCategories,
    string? CoverUrl,
    string? LogoUrl,
    string Slug,
    string DetailPath,
    string? MapsUrl,
    string? WhatsApp,
    string? PublicEmail,
    string? WhatsAppUrl,
    IReadOnlyList<CommercialSchoolPostDto> Posts,
    IReadOnlyList<CommercialSchoolPromotionDto> Promotions);

public sealed record SchoolCommercialProfileDto(
    int SchoolUserId,
    string PublicationStatus,
    string About,
    string LicenseCategories,
    Guid? CoverMediaId,
    Guid? LogoMediaId,
    string? CoverUrl,
    string? LogoUrl,
    string? Slug,
    string? WhatsApp,
    string? PublicEmail,
    bool IsSheetComplete,
    bool IsPublished,
    bool IsCommerciallySuspended,
    string? CommercialSuspensionReason,
    bool PresenceEligible,
    string? PresenceDenyReason,
    DateTime? PublishedAt,
    DateTime UpdatedAt,
    string? PublicPath,
    SchoolCommercialStatsDto? Stats = null);

public sealed record SchoolCommercialStatsDto(
    int PageViews30d,
    int WhatsAppClicks30d,
    int MapsClicks30d,
    int PhoneClicks30d);

public sealed record UpdateSchoolCommercialProfileRequest(
    string About,
    string LicenseCategories,
    Guid? CoverMediaId,
    Guid? LogoMediaId,
    string? WhatsApp = null,
    string? PublicEmail = null,
    string? Slug = null);

public sealed record PublishSchoolCommercialProfileRequest(bool Publish);

public sealed record UpsertSchoolCommercialPostRequest(string Body, Guid? MediaId);

public sealed record UpsertSchoolCommercialPromotionRequest(
    string Title,
    string Body,
    Guid? MediaId,
    DateOnly StartsOn,
    DateOnly EndsOn);

public sealed record SchoolCommercialPostAdminDto(
    int Id,
    string Body,
    Guid? MediaId,
    string? MediaUrl,
    string Status,
    DateTime? PublishedAt,
    DateTime UpdatedAt);

public sealed record SchoolCommercialPromotionAdminDto(
    int Id,
    string Title,
    string Body,
    Guid? MediaId,
    string? MediaUrl,
    DateOnly StartsOn,
    DateOnly EndsOn,
    string Status,
    DateTime UpdatedAt);

public sealed record RecordCommercialEventRequest(string EventType);

public sealed record AdminCommercialSuspendRequest(string Reason);

public sealed record AdminCommercialPresenceDto(
    int SchoolUserId,
    string LegalName,
    string? Slug,
    string PublicationStatus,
    bool IsPublished,
    bool IsCommerciallySuspended,
    string? CommercialSuspensionReason,
    bool PresenceEligible,
    string? PresenceDenyReason);
