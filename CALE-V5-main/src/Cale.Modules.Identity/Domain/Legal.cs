using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Identity.Domain;

/// <summary>Documentos legales de la plataforma. El catálogo es fijo; sus textos se versionan en BD.</summary>
public sealed record LegalDocumentInfo(string Key, string Title, string RequiredAction, IReadOnlyList<string> RequiredForRoles);

public static class LegalDocuments
{
    public const string Terms = "terms";
    public const string Privacy = "privacy";
    public const string DataProcessing = "data-processing";
    public const string AiPolicy = "ai-policy";
    public const string SchoolTerms = "school-terms";

    private static readonly string[] Everyone = [Roles.Student, Roles.Teacher, Roles.School];

    public static readonly IReadOnlyList<LegalDocumentInfo> All =
    [
        new(Terms, "Términos y Condiciones", LegalAcceptanceActions.Accepted, Everyone),
        new(Privacy, "Aviso de Privacidad", LegalAcceptanceActions.Acknowledged, Everyone),
        new(DataProcessing, "Política de Tratamiento de Datos Personales", LegalAcceptanceActions.Authorized, Everyone),
        new(AiPolicy, "Política de uso de Inteligencia Artificial", LegalAcceptanceActions.Acknowledged, []),
        new(SchoolTerms, "Condiciones para escuelas (CEA)", LegalAcceptanceActions.Accepted, [Roles.School])
    ];

    public static LegalDocumentInfo? Find(string? key) =>
        All.FirstOrDefault(d => string.Equals(d.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<LegalDocumentInfo> RequiredFor(string role)
    {
        var normalized = Roles.Normalize(role);
        return All.Where(d => d.RequiredForRoles.Contains(normalized)).ToList();
    }
}

public static class LegalAcceptanceActions
{
    /// <summary>Aceptación contractual (términos, condiciones para escuelas).</summary>
    public const string Accepted = "Accepted";
    /// <summary>Declaración de haber leído (aviso de privacidad).</summary>
    public const string Acknowledged = "Acknowledged";
    /// <summary>Autorización de tratamiento de datos personales.</summary>
    public const string Authorized = "Authorized";
}

public static class LegalAcceptanceMethods
{
    public const string Registration = "Registration";
    public const string Prompt = "Prompt";
}

public static class LegalVersionStatuses
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Retired = "Retired";
}

public static class LegalAcceptanceStatuses
{
    public const string Accepted = "ACCEPTED";
    public const string Outdated = "OUTDATED";
    public const string NotVerified = "NOT_VERIFIED";
}

public sealed partial class LegalDocumentVersion
{
    public const int VersionMax = 40;
    public const int TitleMax = 200;
    public const int SummaryMax = 500;
    public const int ContentMax = 200_000;

    public int Id { get; private set; }
    public string DocumentKey { get; private set; } = "";
    public string Version { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string Content { get; private set; } = "";
    public string ContentHash { get; private set; } = "";
    public string Status { get; private set; } = LegalVersionStatuses.Draft;
    public bool RequiresReacceptance { get; private set; }
    public string? ChangeSummary { get; private set; }
    public int? CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public int? PublishedByUserId { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    private LegalDocumentVersion()
    {
    }

    /// <summary>Contiene espacios sin completar ([... PENDIENTE], [CONFIRMAR], [REVISIÓN JURÍDICA REQUERIDA]...).</summary>
    public bool HasPlaceholders => PlaceholderPattern().IsMatch(Content);

    public static LegalDocumentVersion CreateDraft(
        string documentKey,
        string version,
        string title,
        string content,
        string? changeSummary,
        bool requiresReacceptance,
        int? actorUserId,
        DateTime utcNow)
    {
        var doc = LegalDocuments.Find(documentKey)
            ?? throw new DomainException("Documento legal desconocido.", 400, "legal_document_unknown");
        var entity = new LegalDocumentVersion
        {
            DocumentKey = doc.Key,
            CreatedByUserId = actorUserId,
            CreatedAt = utcNow
        };
        entity.Apply(version, title, content, changeSummary, requiresReacceptance);
        return entity;
    }

    public void UpdateDraft(string version, string title, string content, string? changeSummary, bool requiresReacceptance)
    {
        if (Status != LegalVersionStatuses.Draft)
        {
            throw new DomainException(
                "Una versión publicada no se puede editar. Crea una versión nueva.",
                409,
                "legal_version_locked");
        }

        Apply(version, title, content, changeSummary, requiresReacceptance);
    }

    public void Publish(int? actorUserId, DateTime utcNow)
    {
        if (Status != LegalVersionStatuses.Draft)
        {
            throw new DomainException("Solo se publican borradores.", 409, "legal_version_locked");
        }

        Status = LegalVersionStatuses.Published;
        PublishedByUserId = actorUserId;
        PublishedAt = utcNow;
    }

    public void Retire()
    {
        if (Status == LegalVersionStatuses.Published)
        {
            Status = LegalVersionStatuses.Retired;
        }
    }

    public static string Hash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private void Apply(string version, string title, string content, string? changeSummary, bool requiresReacceptance)
    {
        Version = Required(version, VersionMax, "Indica la versión (por ejemplo 1.1).");
        Title = Required(title, TitleMax, "Indica el título del documento.");
        var body = content?.Trim() ?? "";
        if (body.Length == 0 || body.Length > ContentMax)
        {
            throw new DomainException("El contenido es obligatorio (máximo 200.000 caracteres).", 400, "legal_content_invalid");
        }

        Content = body;
        ContentHash = Hash(body);
        ChangeSummary = string.IsNullOrWhiteSpace(changeSummary)
            ? null
            : changeSummary.Trim()[..Math.Min(changeSummary.Trim().Length, SummaryMax)];
        RequiresReacceptance = requiresReacceptance;
    }

    private static string Required(string? value, int max, string message)
    {
        var trimmed = value?.Trim() ?? "";
        if (trimmed.Length == 0 || trimmed.Length > max)
        {
            throw new DomainException(message, 400, "legal_field_invalid");
        }

        return trimmed;
    }

    [GeneratedRegex(@"\[[^\]]*(PENDIENTE|CONFIRMAR|REVISI[ÓO]N JUR[ÍI]DICA|VERIFICAR)[^\]]*\]", RegexOptions.IgnoreCase)]
    private static partial Regex PlaceholderPattern();
}

/// <summary>Evidencia de aceptación. Nunca se edita ni se borra; sin contraseñas ni datos de más.</summary>
public sealed class UserLegalAcceptance
{
    public const int IpMax = 64;
    public const int UserAgentMax = 300;

    public int Id { get; private set; }
    public int UserId { get; private set; }
    public string DocumentKey { get; private set; } = "";
    public int DocumentVersionId { get; private set; }
    public string Version { get; private set; } = "";
    public string ContentHash { get; private set; } = "";
    public string Action { get; private set; } = "";
    public string Method { get; private set; } = "";
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private UserLegalAcceptance()
    {
    }

    public static UserLegalAcceptance Record(
        int userId,
        LegalDocumentVersion version,
        string action,
        string method,
        string? ipAddress,
        string? userAgent,
        DateTime utcNow) =>
        new()
        {
            UserId = userId,
            DocumentKey = version.DocumentKey,
            DocumentVersionId = version.Id,
            Version = version.Version,
            ContentHash = version.ContentHash,
            Action = action,
            Method = method,
            IpAddress = Clip(ipAddress, IpMax),
            UserAgent = Clip(userAgent, UserAgentMax),
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
