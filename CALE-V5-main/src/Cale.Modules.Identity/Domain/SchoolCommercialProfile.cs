using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Identity.Domain;

/// <summary>
/// Opt-in commercial presence for a school (directory / future public profile).
/// Distinct from operational membership (<see cref="SchoolProfile"/>).
/// No row means the school has never configured commercial presence.
/// </summary>
public sealed class SchoolCommercialProfile
{
    public const int AboutMinLength = 40;
    public const int AboutMaxLength = 1200;
    public const int CategoriesMaxLength = 120;
    public const int SlugMaxLength = 80;
    public const int SuspensionReasonMaxLength = 500;
    public const int WhatsAppMaxLength = 32;
    public const int PublicEmailMaxLength = 320;

    public int SchoolUserId { get; private set; }
    public string PublicationStatus { get; private set; } = SchoolCommercialPublicationStatus.Draft;
    public string About { get; private set; } = "";
    public string LicenseCategories { get; private set; } = "";
    public Guid? CoverMediaId { get; private set; }
    public Guid? LogoMediaId { get; private set; }
    public string? Slug { get; private set; }
    public string? WhatsApp { get; private set; }
    public string? PublicEmail { get; private set; }
    public DateTime? CommerciallySuspendedAt { get; private set; }
    public string? CommercialSuspensionReason { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private SchoolCommercialProfile()
    {
    }

    public static SchoolCommercialProfile Create(int schoolUserId, DateTime utcNow) =>
        new()
        {
            SchoolUserId = schoolUserId,
            PublicationStatus = SchoolCommercialPublicationStatus.Draft,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

    public bool IsPublished =>
        PublicationStatus == SchoolCommercialPublicationStatus.Published;

    public bool IsCommerciallySuspended =>
        CommerciallySuspendedAt is not null;

    public bool HasMedia => CoverMediaId is not null || LogoMediaId is not null;

    public bool IsSheetComplete =>
        About.Trim().Length >= AboutMinLength
        && !string.IsNullOrWhiteSpace(LicenseCategories)
        && HasMedia;

    public void UpdateSheet(
        string about,
        string licenseCategories,
        Guid? coverMediaId,
        Guid? logoMediaId,
        DateTime utcNow)
    {
        About = RequireAbout(about);
        LicenseCategories = RequireCategories(licenseCategories);
        if (coverMediaId is null && logoMediaId is null)
        {
            throw new DomainException(
                "Sube al menos una imagen de portada o un logo comercial.",
                400,
                "commercial_media_required");
        }

        CoverMediaId = coverMediaId;
        LogoMediaId = logoMediaId;
        UpdatedAt = utcNow;
    }

    public void SetPublicContact(string? whatsApp, string? publicEmail, DateTime utcNow)
    {
        WhatsApp = NormalizeWhatsApp(whatsApp);
        PublicEmail = NormalizePublicEmail(publicEmail);
        UpdatedAt = utcNow;
    }

    public void SetSlug(string slug, DateTime utcNow)
    {
        Slug = NormalizeSlug(slug);
        UpdatedAt = utcNow;
    }

    public void Publish(DateTime utcNow)
    {
        if (!IsSheetComplete)
        {
            throw new DomainException(
                "Completa la ficha comercial antes de publicarla.",
                400,
                "commercial_sheet_incomplete");
        }

        if (IsCommerciallySuspended)
        {
            throw new DomainException(
                "La presencia comercial está suspendida por la plataforma.",
                403,
                "commercial_suspended");
        }

        PublicationStatus = SchoolCommercialPublicationStatus.Published;
        PublishedAt ??= utcNow;
        UpdatedAt = utcNow;
    }

    public void Unpublish(DateTime utcNow)
    {
        PublicationStatus = SchoolCommercialPublicationStatus.Unpublished;
        UpdatedAt = utcNow;
    }

    public void SuspendCommercial(string reason, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(
                "Indica el motivo de la suspensión comercial.",
                400,
                "commercial_suspension_reason_required");
        }

        var trimmed = reason.Trim();
        if (trimmed.Length > SuspensionReasonMaxLength)
        {
            throw new DomainException(
                $"El motivo es muy largo (máximo {SuspensionReasonMaxLength} caracteres).",
                400,
                "commercial_suspension_reason_too_long");
        }

        CommerciallySuspendedAt = utcNow;
        CommercialSuspensionReason = trimmed;
        if (IsPublished)
        {
            PublicationStatus = SchoolCommercialPublicationStatus.Unpublished;
        }

        UpdatedAt = utcNow;
    }

    public void ClearCommercialSuspension(DateTime utcNow)
    {
        CommerciallySuspendedAt = null;
        CommercialSuspensionReason = null;
        UpdatedAt = utcNow;
    }

    private static string RequireAbout(string? about)
    {
        var trimmed = (about ?? "").Trim();
        if (trimmed.Length < AboutMinLength)
        {
            throw new DomainException(
                $"La descripción comercial debe tener al menos {AboutMinLength} caracteres.",
                400,
                "commercial_about_too_short");
        }

        if (trimmed.Length > AboutMaxLength)
        {
            throw new DomainException(
                $"La descripción comercial es muy larga (máximo {AboutMaxLength} caracteres).",
                400,
                "commercial_about_too_long");
        }

        return trimmed;
    }

    private static string RequireCategories(string? categories)
    {
        var trimmed = (categories ?? "").Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new DomainException(
                "Indica al menos una categoría de licencia (por ejemplo A2, B1).",
                400,
                "commercial_categories_required");
        }

        if (trimmed.Length > CategoriesMaxLength)
        {
            throw new DomainException(
                $"Las categorías son muy largas (máximo {CategoriesMaxLength} caracteres).",
                400,
                "commercial_categories_too_long");
        }

        return trimmed;
    }

    private static string? NormalizeWhatsApp(string? raw)
    {
        var digits = new string((raw ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length == 0)
        {
            return null;
        }

        if (digits.Length is < 10 or > WhatsAppMaxLength)
        {
            throw new DomainException(
                "El WhatsApp debe tener entre 10 y 32 dígitos (incluye indicativo).",
                400,
                "invalid_commercial_whatsapp");
        }

        return digits;
    }

    private static string? NormalizePublicEmail(string? raw)
    {
        var trimmed = (raw ?? "").Trim().ToLowerInvariant();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length > PublicEmailMaxLength || !trimmed.Contains('@'))
        {
            throw new DomainException(
                "El correo público no es válido.",
                400,
                "invalid_commercial_email");
        }

        return trimmed;
    }

    public static string NormalizeSlug(string? raw)
    {
        var source = (raw ?? "").Trim().ToLowerInvariant();
        if (source.Length == 0)
        {
            throw new DomainException("El slug comercial es obligatorio.", 400, "commercial_slug_required");
        }

        var chars = source
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        slug = slug.Trim('-');
        if (slug.Length == 0)
        {
            throw new DomainException("El slug comercial no es válido.", 400, "invalid_commercial_slug");
        }

        if (slug.Length > SlugMaxLength)
        {
            slug = slug[..SlugMaxLength].TrimEnd('-');
        }

        return slug;
    }
}

public static class SchoolCommercialPublicationStatus
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Unpublished = "Unpublished";
}
