using System.Security.Cryptography;
using System.Text;
using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Identity.Domain;

public static class AgePolicy
{
    public const int AdultAge = 18;
    public const int MinAge = 16;
    public const int MaxAge = 100;

    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    public static DateOnly TodayInColombia(DateTime utcNow) =>
        DateOnly.FromDateTime(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc).Add(ColombiaOffset));

    public static int AgeOn(DateOnly birthDate, DateOnly today)
    {
        var age = today.Year - birthDate.Year;
        if (today < birthDate.AddYears(age))
        {
            age--;
        }

        return age;
    }

    public static void EnsureValid(DateOnly? birthDate, DateTime utcNow)
    {
        if (birthDate is not { } date)
        {
            throw new DomainException("Indica tu fecha de nacimiento.", 400, "birth_date_required");
        }

        var today = TodayInColombia(utcNow);
        if (date > today || AgeOn(date, today) > MaxAge)
        {
            throw new DomainException("La fecha de nacimiento no es válida.", 400, "birth_date_invalid");
        }

        if (AgeOn(date, today) < MinAge)
        {
            throw new DomainException("Debes tener al menos 16 años para usar Luz Verde.", 400, "age_below_minimum");
        }
    }

    public static bool IsMinor(DateOnly birthDate, DateTime utcNow) =>
        AgeOn(birthDate, TodayInColombia(utcNow)) < AdultAge;
}

public static class GuardianStatuses
{
    /// <summary>El usuario era mayor de edad al declarar su fecha de nacimiento.</summary>
    public const string NotRequired = "NOT_REQUIRED";
    public const string Pending = "PENDING";
    public const string Confirmed = "CONFIRMED";
    public const string Rejected = "REJECTED";
    public const string Revoked = "REVOKED";
}

public static class GuardianRelationships
{
    public static readonly IReadOnlyList<string> All = ["Madre", "Padre", "Tutor legal"];

    public static string? Find(string? value) =>
        All.FirstOrDefault(r => string.Equals(r, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class GuardianEventActions
{
    public const string Declared = "Declared";
    public const string GuardianUpdated = "GuardianUpdated";
    public const string EmailSent = "EmailSent";
    public const string EmailNotSent = "EmailNotSent";
    public const string Confirmed = "Confirmed";
    public const string Rejected = "Rejected";
    public const string Revoked = "Revoked";
}

/// <summary>
/// Fecha de nacimiento declarada por el estudiante y, si es menor, los datos mínimos de su
/// representante legal y el estado de la autorización. Solo el representante decide.
/// </summary>
public sealed class AgeDeclaration
{
    public const int GuardianNameMin = 3;
    public const int GuardianNameMax = 150;
    public const int GuardianEmailMax = 254;
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromDays(30);

    public int Id { get; private set; }
    public int UserId { get; private set; }
    public DateOnly BirthDate { get; private set; }
    public bool IsMinorAtDeclaration { get; private set; }
    public string? GuardianName { get; private set; }
    public string? GuardianRelationship { get; private set; }
    public string? GuardianEmail { get; private set; }
    public string GuardianStatus { get; private set; } = GuardianStatuses.NotRequired;
    public string? GuardianTokenHash { get; private set; }
    public DateTime? GuardianLinkExpiresAt { get; private set; }
    public DateTime? GuardianDecidedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private AgeDeclaration()
    {
    }

    public static AgeDeclaration Declare(int userId, DateOnly birthDate, bool isMinor, DateTime utcNow) =>
        new()
        {
            UserId = userId,
            BirthDate = birthDate,
            IsMinorAtDeclaration = isMinor,
            GuardianStatus = isMinor ? GuardianStatuses.Pending : GuardianStatuses.NotRequired,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

    public bool AwaitsGuardian => GuardianStatus == GuardianStatuses.Pending;

    public void SetGuardian(string name, string relationship, string email, DateTime utcNow)
    {
        if (!AwaitsGuardian)
        {
            throw new DomainException(
                "La autorización del representante ya fue decidida.",
                409,
                "guardian_already_decided");
        }

        GuardianName = name;
        GuardianRelationship = relationship;
        GuardianEmail = email;
        UpdatedAt = utcNow;
    }

    /// <summary>Genera un enlace nuevo; los anteriores dejan de servir. Devuelve el token en claro una sola vez.</summary>
    public string IssueLink(DateTime utcNow)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        GuardianTokenHash = HashToken(token);
        GuardianLinkExpiresAt = utcNow.Add(LinkLifetime);
        UpdatedAt = utcNow;
        return token;
    }

    public void Confirm(DateTime utcNow) => Decide(GuardianStatuses.Pending, GuardianStatuses.Confirmed, utcNow, requireLiveLink: true);

    public void Reject(DateTime utcNow) => Decide(GuardianStatuses.Pending, GuardianStatuses.Rejected, utcNow, requireLiveLink: true);

    /// <summary>La revocación sigue disponible con el mismo enlace aunque haya vencido.</summary>
    public void Revoke(DateTime utcNow) => Decide(GuardianStatuses.Confirmed, GuardianStatuses.Revoked, utcNow, requireLiveLink: false);

    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private void Decide(string from, string to, DateTime utcNow, bool requireLiveLink)
    {
        if (GuardianStatus != from)
        {
            throw new DomainException("Esta autorización ya no admite ese cambio.", 409, "guardian_already_decided");
        }

        if (requireLiveLink && GuardianLinkExpiresAt is { } expires && expires <= utcNow)
        {
            throw new DomainException(
                "El enlace venció. Pide al estudiante que lo envíe de nuevo.",
                410,
                "guardian_link_expired");
        }

        GuardianStatus = to;
        GuardianDecidedAt = utcNow;
        UpdatedAt = utcNow;
    }
}

/// <summary>Trazabilidad de la autorización del representante. Nunca se edita ni se borra.</summary>
public sealed class GuardianConsentEvent
{
    public const int NoteMax = 300;

    public int Id { get; private set; }
    public int AgeDeclarationId { get; private set; }
    public int UserId { get; private set; }
    public string Action { get; private set; } = "";
    public string? DocumentVersions { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private GuardianConsentEvent()
    {
    }

    /// <param name="documentVersions">Versiones vigentes al decidir, p. ej. "privacy@1.0;data-processing@1.0".</param>
    public static GuardianConsentEvent Record(
        AgeDeclaration declaration,
        string action,
        DateTime utcNow,
        string? documentVersions = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? note = null) =>
        new()
        {
            AgeDeclarationId = declaration.Id,
            UserId = declaration.UserId,
            Action = action,
            DocumentVersions = Clip(documentVersions, 300),
            IpAddress = Clip(ipAddress, UserLegalAcceptance.IpMax),
            UserAgent = Clip(userAgent, UserLegalAcceptance.UserAgentMax),
            Note = Clip(note, NoteMax),
            CreatedAt = utcNow
        };

    private static string? Clip(string? value, int max)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
