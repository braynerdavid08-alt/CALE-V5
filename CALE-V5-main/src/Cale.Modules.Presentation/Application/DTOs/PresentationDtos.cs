namespace Cale.Modules.Presentation.Application.DTOs;

public sealed record PresentationListItemDto(
    int Id,
    string Title,
    string? Description,
    string Category,
    int? GroupId,
    string? ThumbnailUrl,
    int SlideCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record PresentationSummaryDto(
    int Total,
    PresentationListItemDto? Latest);

public sealed record PresentationSlideDto(
    int Id,
    int Position,
    string Title,
    string? Notes,
    string BackgroundJson,
    string ElementsJson);

public sealed record PresentationDetailDto(
    int Id,
    string Title,
    string? Description,
    string Category,
    int? GroupId,
    int? SchoolId,
    string? ThumbnailUrl,
    int SlideCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int UpdatedByUserId,
    IReadOnlyList<PresentationSlideDto> Slides);

public sealed record CreatePresentationRequest(
    string Title,
    string? Description,
    string? Category,
    int? GroupId,
    string? TemplateKey);

public sealed record UpdatePresentationMetaRequest(
    string Title,
    string? Description,
    string? Category,
    int? GroupId);

public sealed record SavePresentationDocumentRequest(
    string Title,
    string? Description,
    string? Category,
    int? GroupId,
    string? ThumbnailUrl,
    IReadOnlyList<SaveSlideRequest> Slides);

public sealed record SaveSlideRequest(
    int? Id,
    string Title,
    string? Notes,
    string BackgroundJson,
    string ElementsJson);
