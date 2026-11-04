using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Identity.Domain;

public static class SchoolCommercialContentStatuses
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Hidden = "Hidden";
}

public sealed class SchoolCommercialPost
{
    public const int BodyMaxLength = 1500;

    public int Id { get; private set; }
    public int SchoolUserId { get; private set; }
    public string Body { get; private set; } = "";
    public Guid? MediaId { get; private set; }
    public string Status { get; private set; } = SchoolCommercialContentStatuses.Draft;
    public DateTime? PublishedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private SchoolCommercialPost()
    {
    }

    public static SchoolCommercialPost Create(
        int schoolUserId,
        string body,
        Guid? mediaId,
        DateTime utcNow)
    {
        var post = new SchoolCommercialPost
        {
            SchoolUserId = schoolUserId,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
        post.Update(body, mediaId, utcNow);
        return post;
    }

    public void Update(string body, Guid? mediaId, DateTime utcNow)
    {
        Body = RequireBody(body);
        MediaId = mediaId;
        UpdatedAt = utcNow;
    }

    public void Publish(DateTime utcNow)
    {
        Status = SchoolCommercialContentStatuses.Published;
        PublishedAt ??= utcNow;
        UpdatedAt = utcNow;
    }

    public void Hide(DateTime utcNow)
    {
        Status = SchoolCommercialContentStatuses.Hidden;
        UpdatedAt = utcNow;
    }

    private static string RequireBody(string? body)
    {
        var trimmed = (body ?? "").Trim();
        if (trimmed.Length < 8)
        {
            throw new DomainException(
                "La publicación debe tener al menos 8 caracteres.",
                400,
                "commercial_post_too_short");
        }

        if (trimmed.Length > BodyMaxLength)
        {
            throw new DomainException(
                $"La publicación es muy larga (máximo {BodyMaxLength} caracteres).",
                400,
                "commercial_post_too_long");
        }

        return trimmed;
    }
}

public sealed class SchoolCommercialPromotion
{
    public const int TitleMaxLength = 120;
    public const int BodyMaxLength = 800;

    public int Id { get; private set; }
    public int SchoolUserId { get; private set; }
    public string Title { get; private set; } = "";
    public string Body { get; private set; } = "";
    public Guid? MediaId { get; private set; }
    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }
    public string Status { get; private set; } = SchoolCommercialContentStatuses.Draft;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private SchoolCommercialPromotion()
    {
    }

    public static SchoolCommercialPromotion Create(
        int schoolUserId,
        string title,
        string body,
        Guid? mediaId,
        DateOnly startsOn,
        DateOnly endsOn,
        DateTime utcNow)
    {
        var promo = new SchoolCommercialPromotion
        {
            SchoolUserId = schoolUserId,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
        promo.Update(title, body, mediaId, startsOn, endsOn, utcNow);
        return promo;
    }

    public void Update(
        string title,
        string body,
        Guid? mediaId,
        DateOnly startsOn,
        DateOnly endsOn,
        DateTime utcNow)
    {
        Title = RequireTitle(title);
        Body = RequireBody(body);
        if (endsOn < startsOn)
        {
            throw new DomainException(
                "La promoción debe terminar el mismo día o después del inicio.",
                400,
                "invalid_promotion_window");
        }

        MediaId = mediaId;
        StartsOn = startsOn;
        EndsOn = endsOn;
        UpdatedAt = utcNow;
    }

    public void Publish(DateTime utcNow)
    {
        Status = SchoolCommercialContentStatuses.Published;
        UpdatedAt = utcNow;
    }

    public void Hide(DateTime utcNow)
    {
        Status = SchoolCommercialContentStatuses.Hidden;
        UpdatedAt = utcNow;
    }

    public bool IsActiveOn(DateOnly day) =>
        Status == SchoolCommercialContentStatuses.Published
        && day >= StartsOn
        && day <= EndsOn;

    private static string RequireTitle(string? title)
    {
        var trimmed = (title ?? "").Trim();
        if (trimmed.Length is 0 or > TitleMaxLength)
        {
            throw new DomainException(
                $"El título de la promoción es obligatorio (máximo {TitleMaxLength}).",
                400,
                "invalid_promotion_title");
        }

        return trimmed;
    }

    private static string RequireBody(string? body)
    {
        var trimmed = (body ?? "").Trim();
        if (trimmed.Length < 8)
        {
            throw new DomainException(
                "La promoción debe tener al menos 8 caracteres.",
                400,
                "commercial_promotion_too_short");
        }

        if (trimmed.Length > BodyMaxLength)
        {
            throw new DomainException(
                $"La promoción es muy larga (máximo {BodyMaxLength} caracteres).",
                400,
                "commercial_promotion_too_long");
        }

        return trimmed;
    }
}

public static class SchoolCommercialEventTypes
{
    public const string PageView = "PageView";
    public const string WhatsAppClick = "WhatsAppClick";
    public const string MapsClick = "MapsClick";
    public const string PhoneClick = "PhoneClick";

    public static bool IsValid(string? value) => value is
        PageView or WhatsAppClick or MapsClick or PhoneClick;
}

public sealed class SchoolCommercialEvent
{
    public long Id { get; private set; }
    public int SchoolUserId { get; private set; }
    public string EventType { get; private set; } = "";
    public DateTime CreatedAt { get; private set; }

    private SchoolCommercialEvent()
    {
    }

    public static SchoolCommercialEvent Create(int schoolUserId, string eventType, DateTime utcNow)
    {
        if (!SchoolCommercialEventTypes.IsValid(eventType))
        {
            throw new DomainException("Tipo de evento inválido.", 400, "invalid_commercial_event");
        }

        return new SchoolCommercialEvent
        {
            SchoolUserId = schoolUserId,
            EventType = eventType,
            CreatedAt = utcNow
        };
    }
}
