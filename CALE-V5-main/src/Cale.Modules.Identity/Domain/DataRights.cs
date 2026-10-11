using System.Security.Cryptography;
using System.Text;
using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Identity.Domain;

/// <summary>Tipo de PQR. Los de datos personales siguen los plazos de la Ley 1581 de 2012.</summary>
public sealed record PqrKindInfo(string Key, string Label, bool IsDataRight, bool IsClaim);

public static class PqrKinds
{
    public const string Consulta = "consulta";
    public const string Rectificacion = "rectificacion";
    public const string Supresion = "supresion";
    public const string Revocatoria = "revocatoria";
    public const string Reclamo = "reclamo";
    public const string Peticion = "peticion";
    public const string Queja = "queja";
    public const string Seguridad = "seguridad";
    public const string Suspension = "suspension";

    public static readonly IReadOnlyList<PqrKindInfo> All =
    [
        new(Consulta, "Consulta sobre mis datos personales", true, false),
        new(Rectificacion, "Actualizar o corregir mis datos", true, true),
        new(Supresion, "Eliminar mis datos", true, true),
        new(Revocatoria, "Revocar mi autorización de tratamiento de datos", true, true),
        new(Reclamo, "Reclamo por el manejo de mis datos", true, true),
        new(Peticion, "Petición", false, false),
        new(Queja, "Queja", false, false),
        new(Seguridad, "Reporte de un problema de seguridad", false, false),
        new(Suspension, "Revisión de la suspensión de mi cuenta", false, false)
    ];

    public static PqrKindInfo? Find(string? key) =>
        All.FirstOrDefault(k => string.Equals(k.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public static class PqrStatuses
{
    public const string Pending = "PENDIENTE";
    public const string InReview = "EN_REVISION";
    public const string Answered = "RESPONDIDA";
    public const string Rejected = "RECHAZADA";
    public const string Escalated = "ESCALADA";

    public static readonly IReadOnlyList<string> All = [Pending, InReview, Answered, Rejected, Escalated];

    public static bool IsClosed(string status) => status is Answered or Rejected;
}

public static class PqrEventActions
{
    public const string Created = "Created";
    public const string StatusChanged = "StatusChanged";
    public const string Extended = "Extended";
    public const string Note = "Note";
}

/// <summary>
/// Plazos en días hábiles (lunes a viernes). No descuenta festivos, así que el vencimiento
/// calculado nunca es posterior al legal.
/// </summary>
public static class PqrDeadlines
{
    /// <summary>Ley 1581, art. 14: consultas en 10 días hábiles, prorrogables 5.</summary>
    public const int ConsultaDays = 10;
    public const int ConsultaExtension = 5;
    /// <summary>Ley 1581, art. 15: reclamos en 15 días hábiles, prorrogables 8.</summary>
    public const int ClaimDays = 15;
    public const int ClaimExtension = 8;
    /// <summary>Objetivo interno para lo que no es de datos personales.</summary>
    public const int InternalDays = 15;
    public const int InternalExtension = 8;

    public static (int Days, int Extension) For(PqrKindInfo kind) =>
        kind.IsDataRight
            ? kind.IsClaim ? (ClaimDays, ClaimExtension) : (ConsultaDays, ConsultaExtension)
            : (InternalDays, InternalExtension);

    public static DateTime AddBusinessDays(DateTime utc, int days)
    {
        var colombia = utc.AddHours(-5);
        var date = colombia.Date;
        var added = 0;
        while (added < days)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                added++;
            }
        }

        // Fin del día hábil en Colombia, expresado en UTC.
        return DateTime.SpecifyKind(date.AddDays(1).AddHours(5).AddTicks(-1), DateTimeKind.Utc);
    }
}

/// <summary>PQR o solicitud del titular de datos. Nunca se borra; cada cambio queda en su historial.</summary>
public sealed class PqrRequest
{
    public const int NameMax = 150;
    public const int EmailMax = 254;
    public const int SubjectMax = 200;
    public const int MessageMin = 20;
    public const int MessageMax = 4000;
    public const int ResponseMin = 10;
    public const int ResponseMax = 4000;

    public int Id { get; private set; }
    public string Kind { get; private set; } = PqrKinds.Peticion;
    public string Status { get; private set; } = PqrStatuses.Pending;
    public int? UserId { get; private set; }
    public string ContactName { get; private set; } = "";
    public string ContactEmail { get; private set; } = "";
    public string Subject { get; private set; } = "";
    public string Message { get; private set; } = "";
    public string? Response { get; private set; }
    public string TrackingCodeHash { get; private set; } = "";
    public DateTime DueAt { get; private set; }
    public bool Extended { get; private set; }
    public int? AssignedAdminId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    private PqrRequest()
    {
    }

    public string Radicado => $"PQR-{CreatedAt:yyyy}-{Id:D6}";

    public bool IsClosed => PqrStatuses.IsClosed(Status);

    public static (PqrRequest Request, string TrackingCode) Create(
        PqrKindInfo kind,
        int? userId,
        string contactName,
        string contactEmail,
        string subject,
        string message,
        DateTime utcNow)
    {
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(12));
        var (days, _) = PqrDeadlines.For(kind);
        var request = new PqrRequest
        {
            Kind = kind.Key,
            UserId = userId,
            ContactName = contactName,
            ContactEmail = contactEmail,
            Subject = subject,
            Message = message,
            TrackingCodeHash = HashCode(code),
            DueAt = PqrDeadlines.AddBusinessDays(utcNow, days),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
        return (request, code);
    }

    public static string HashCode(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant()))).ToLowerInvariant();

    public void ChangeStatus(string status, string? response, int adminId, DateTime utcNow)
    {
        if (!PqrStatuses.All.Contains(status))
        {
            throw new DomainException("Estado no válido.", 400, "pqr_status_invalid");
        }

        if (IsClosed)
        {
            throw new DomainException("Esta solicitud ya está cerrada.", 409, "pqr_closed");
        }

        var text = response?.Trim();
        if (PqrStatuses.IsClosed(status) && (text is null || text.Length < ResponseMin))
        {
            throw new DomainException(
                status == PqrStatuses.Rejected
                    ? "Para rechazar escribe la justificación (mínimo 10 caracteres)."
                    : "Escribe la respuesta para el solicitante (mínimo 10 caracteres).",
                400,
                "pqr_response_required");
        }

        if (text is { Length: > ResponseMax })
        {
            throw new DomainException("La respuesta admite hasta 4000 caracteres.", 400, "pqr_response_too_long");
        }

        Status = status;
        AssignedAdminId = adminId;
        if (!string.IsNullOrEmpty(text) && PqrStatuses.IsClosed(status))
        {
            Response = text;
            ClosedAt = utcNow;
        }

        UpdatedAt = utcNow;
    }

    public void Extend(DateTime utcNow)
    {
        if (IsClosed)
        {
            throw new DomainException("Esta solicitud ya está cerrada.", 409, "pqr_closed");
        }

        if (Extended)
        {
            throw new DomainException("El plazo ya se prorrogó una vez.", 409, "pqr_already_extended");
        }

        var (_, extension) = PqrDeadlines.For(PqrKinds.Find(Kind)!);
        DueAt = PqrDeadlines.AddBusinessDays(DueAt, extension);
        Extended = true;
        UpdatedAt = utcNow;
    }
}

/// <summary>Historial de una PQR. Nunca se edita ni se borra.</summary>
public sealed class PqrEvent
{
    public const int NoteMax = 4000;

    public int Id { get; private set; }
    public int PqrRequestId { get; private set; }
    public string Action { get; private set; } = "";
    public string? FromStatus { get; private set; }
    public string? ToStatus { get; private set; }
    public string? Note { get; private set; }
    public int? ActorUserId { get; private set; }
    public bool VisibleToRequester { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PqrEvent()
    {
    }

    public static PqrEvent Record(
        PqrRequest request,
        string action,
        DateTime utcNow,
        int? actorUserId = null,
        string? fromStatus = null,
        string? toStatus = null,
        string? note = null,
        bool visibleToRequester = true) =>
        new()
        {
            PqrRequestId = request.Id,
            Action = action,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, NoteMax)],
            ActorUserId = actorUserId,
            VisibleToRequester = visibleToRequester,
            CreatedAt = utcNow
        };
}
