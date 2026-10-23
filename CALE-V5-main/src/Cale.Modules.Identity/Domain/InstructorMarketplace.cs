using Cale.BuildingBlocks.Domain.Exceptions;

namespace Cale.Modules.Identity.Domain;

/// <summary>
/// A student asks a listed instructor for private classes. Luz Verde only connects them:
/// there is no payment or contract inside the app.
/// </summary>
public sealed class PrivateClassRequest
{
    public const int ScheduleMax = 200;
    public const int MessageMin = 10;
    public const int MessageMax = 1000;
    public const int ReplyMin = 5;
    public const int ReplyMax = 1000;

    public int Id { get; private set; }
    public int InstructorUserId { get; private set; }
    public int StudentUserId { get; private set; }
    public string Category { get; private set; } = PrivateClassCategories.B1;
    public string PreferredSchedule { get; private set; } = "";
    public string Message { get; private set; } = "";
    public string Status { get; private set; } = PrivateClassStatuses.Pending;
    public string? Reply { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? RespondedAt { get; private set; }

    private PrivateClassRequest()
    {
    }

    public static PrivateClassRequest Create(
        int instructorUserId,
        int studentUserId,
        string? category,
        string? preferredSchedule,
        string? message,
        DateTime utcNow)
    {
        return new PrivateClassRequest
        {
            InstructorUserId = instructorUserId,
            StudentUserId = studentUserId,
            Category = PrivateClassCategories.Normalize(category),
            PreferredSchedule = Text(preferredSchedule, 1, ScheduleMax, "Cuéntale al instructor qué días y horas te sirven.", "invalid_schedule"),
            Message = Text(message, MessageMin, MessageMax, $"Escribe un mensaje de al menos {MessageMin} caracteres.", "invalid_message"),
            CreatedAt = utcNow
        };
    }

    public void Respond(bool accept, string? reply, DateTime utcNow)
    {
        EnsurePending();
        Reply = accept
            ? Text(reply, ReplyMin, ReplyMax, "Escribe cómo y cuándo vas a contactar al estudiante.", "invalid_reply")
            : string.IsNullOrWhiteSpace(reply) ? null : Text(reply, 1, ReplyMax, "", "invalid_reply");
        Status = accept ? PrivateClassStatuses.Accepted : PrivateClassStatuses.Declined;
        RespondedAt = utcNow;
    }

    public void Cancel(DateTime utcNow)
    {
        EnsurePending();
        Status = PrivateClassStatuses.Cancelled;
        RespondedAt = utcNow;
    }

    private void EnsurePending()
    {
        if (Status != PrivateClassStatuses.Pending)
        {
            throw new DomainException("Esta solicitud ya fue respondida.", 400, "private_class_closed");
        }
    }

    private static string Text(string? value, int min, int max, string message, string code)
    {
        var trimmed = (value ?? "").Trim();
        if (trimmed.Length < min || trimmed.Length > max)
        {
            throw new DomainException(
                trimmed.Length > max ? $"El texto es muy largo (máximo {max} caracteres)." : message,
                400,
                code);
        }

        return trimmed;
    }
}

public static class PrivateClassStatuses
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Declined = "Declined";
    public const string Cancelled = "Cancelled";
}

public static class PrivateClassCategories
{
    public const string A2 = "A2";
    public const string B1 = "B1";
    public const string C1 = "C1";
    public const string Other = "Otra";

    public static readonly string[] All = [A2, B1, C1, Other];

    public static string Normalize(string? value) =>
        All.FirstOrDefault(c => c.Equals((value ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new DomainException("Elige la categoría: A2, B1, C1 u Otra.", 400, "invalid_category");
}

/// <summary>One rating per user and instructor; editing replaces it. Admins can hide abusive comments.</summary>
public sealed class InstructorRating
{
    public const int CommentMax = 500;

    public int Id { get; private set; }
    public int InstructorUserId { get; private set; }
    public int RaterUserId { get; private set; }
    public int Stars { get; private set; }
    public string? Comment { get; private set; }
    public bool Hidden { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private InstructorRating()
    {
    }

    public static InstructorRating Create(int instructorUserId, int raterUserId, int stars, string? comment, DateTime utcNow)
    {
        var rating = new InstructorRating
        {
            InstructorUserId = instructorUserId,
            RaterUserId = raterUserId,
            CreatedAt = utcNow
        };
        rating.Update(stars, comment, utcNow);
        return rating;
    }

    public void Update(int stars, string? comment, DateTime utcNow)
    {
        if (stars is < 1 or > 5)
        {
            throw new DomainException("La calificación va de 1 a 5 estrellas.", 400, "invalid_stars");
        }

        var trimmed = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (trimmed is { Length: > CommentMax })
        {
            throw new DomainException($"El comentario es muy largo (máximo {CommentMax} caracteres).", 400, "invalid_comment");
        }

        Stars = stars;
        Comment = trimmed;
        UpdatedAt = utcNow;
    }

    public void SetHidden(bool hidden) => Hidden = hidden;
}
