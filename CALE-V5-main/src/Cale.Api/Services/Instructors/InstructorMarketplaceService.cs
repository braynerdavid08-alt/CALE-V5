using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Api.Services.Instructors;

/// <summary>
/// Private-class requests and ratings for instructors listed in the public directory.
/// Luz Verde only connects student and instructor; contact details are shared once the
/// instructor accepts, and nothing is paid inside the app.
/// </summary>
public sealed class InstructorMarketplaceService
{
    public const int MaxPendingPerStudent = 3;
    public const int ReviewsShown = 20;

    private readonly CaleDbContext _db;
    private readonly IClock _clock;
    private readonly INotificationPublisher _notifications;

    public InstructorMarketplaceService(CaleDbContext db, IClock clock, INotificationPublisher notifications)
    {
        _db = db;
        _clock = clock;
        _notifications = notifications;
    }

    public async Task<IReadOnlyList<InstructorReviewDto>> ListReviewsAsync(int instructorId, bool includeHidden, CancellationToken ct)
    {
        await EnsureListedAsync(instructorId, ct);
        var rows = await _db.Set<InstructorRating>().AsNoTracking()
            .Where(r => r.InstructorUserId == instructorId && r.Comment != null && (includeHidden || !r.Hidden))
            .OrderByDescending(r => r.UpdatedAt)
            .Take(ReviewsShown)
            .Join(_db.Set<User>(), r => r.RaterUserId, u => u.Id, (r, u) => new { r, u.Name })
            .ToListAsync(ct);
        return rows.Select(x => new InstructorReviewDto(
            x.r.Id,
            x.r.Stars,
            x.r.Comment!,
            HomepageService.PublicDisplayName(x.Name),
            x.r.UpdatedAt,
            x.r.Hidden)).ToList();
    }

    public async Task<MyInstructorRatingDto?> GetMyRatingAsync(int raterId, int instructorId, CancellationToken ct)
    {
        var row = await _db.Set<InstructorRating>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.InstructorUserId == instructorId && r.RaterUserId == raterId, ct);
        return row is null ? null : new MyInstructorRatingDto(row.Stars, row.Comment);
    }

    public async Task<MyInstructorRatingDto> RateAsync(int raterId, int instructorId, RateInstructorRequest request, CancellationToken ct)
    {
        if (raterId == instructorId)
        {
            throw new DomainException("No puedes calificarte a ti mismo.", 400, "self_rating");
        }

        await EnsureListedAsync(instructorId, ct);
        var now = _clock.UtcNow;
        var row = await _db.Set<InstructorRating>()
            .FirstOrDefaultAsync(r => r.InstructorUserId == instructorId && r.RaterUserId == raterId, ct);
        if (row is null)
        {
            row = InstructorRating.Create(instructorId, raterId, request.Stars, request.Comment, now);
            _db.Add(row);
        }
        else
        {
            row.Update(request.Stars, request.Comment, now);
        }

        await _db.SaveChangesAsync(ct);
        return new MyInstructorRatingDto(row.Stars, row.Comment);
    }

    public async Task SetReviewHiddenAsync(int ratingId, bool hidden, CancellationToken ct)
    {
        var row = await _db.Set<InstructorRating>().FirstOrDefaultAsync(r => r.Id == ratingId, ct)
            ?? throw new NotFoundException("Calificación no encontrada.", "rating_not_found");
        row.SetHidden(hidden);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PrivateClassRequestDto> RequestClassAsync(int studentId, CreatePrivateClassRequest request, CancellationToken ct)
    {
        var listing = await _db.Set<InstructorListing>().AsNoTracking()
            .FirstOrDefaultAsync(l => l.UserId == request.InstructorId, ct);
        var instructor = await _db.Set<User>().AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.InstructorId && u.IsActive && u.Role == Roles.Teacher, ct);
        if (listing is null || instructor is null)
        {
            throw new NotFoundException("Instructor no encontrado.", "instructor_not_found");
        }

        if (!listing.AcceptsPrivateClasses)
        {
            throw new DomainException("Este instructor no está recibiendo solicitudes de clases particulares.", 400, "private_classes_closed");
        }

        var pending = await _db.Set<PrivateClassRequest>()
            .Where(r => r.StudentUserId == studentId && r.Status == PrivateClassStatuses.Pending)
            .Select(r => r.InstructorUserId)
            .ToListAsync(ct);
        if (pending.Contains(request.InstructorId))
        {
            throw new DomainException("Ya tienes una solicitud pendiente con este instructor.", 400, "private_class_duplicate");
        }

        if (pending.Count >= MaxPendingPerStudent)
        {
            throw new DomainException(
                $"Puedes tener máximo {MaxPendingPerStudent} solicitudes pendientes. Espera respuesta o cancela alguna.",
                400,
                "private_class_limit");
        }

        var entity = PrivateClassRequest.Create(
            request.InstructorId, studentId, request.Category, request.PreferredSchedule, request.Message, _clock.UtcNow);
        _db.Add(entity);
        await _db.SaveChangesAsync(ct);

        var student = await _db.Set<User>().AsNoTracking().FirstAsync(u => u.Id == studentId, ct);
        await _notifications.NotifyUsersAsync(
            [request.InstructorId],
            new NotificationDraft(
                "Nueva solicitud de clase particular",
                $"{HomepageService.PublicDisplayName(student.Name)} quiere clases particulares (categoría {entity.Category}).",
                NotificationTypes.System,
                RelatedEntity: "private_class_request",
                RelatedId: entity.Id,
                Link: "/teacher/clases-particulares",
                Priority: NotificationPriorities.High,
                DedupeKey: $"private-class-{entity.Id}"),
            ct);

        return (await MapAsync([entity], ct))[0];
    }

    public async Task<IReadOnlyList<PrivateClassRequestDto>> ListForStudentAsync(int studentId, CancellationToken ct)
    {
        var rows = await _db.Set<PrivateClassRequest>().AsNoTracking()
            .Where(r => r.StudentUserId == studentId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync(ct);
        return await MapAsync(rows, ct);
    }

    public async Task<IReadOnlyList<PrivateClassRequestDto>> ListForInstructorAsync(int instructorId, CancellationToken ct)
    {
        var rows = await _db.Set<PrivateClassRequest>().AsNoTracking()
            .Where(r => r.InstructorUserId == instructorId && r.Status != PrivateClassStatuses.Cancelled)
            .OrderByDescending(r => r.Status == PrivateClassStatuses.Pending)
            .ThenByDescending(r => r.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        return await MapAsync(rows, ct);
    }

    public async Task<PrivateClassRequestDto> RespondAsync(int instructorId, int requestId, RespondPrivateClassRequest request, CancellationToken ct)
    {
        var entity = await _db.Set<PrivateClassRequest>().FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (entity is null || entity.InstructorUserId != instructorId)
        {
            throw new NotFoundException("Solicitud no encontrada.", "private_class_not_found");
        }

        entity.Respond(request.Accept, request.Reply, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);

        var instructor = await _db.Set<User>().AsNoTracking().FirstAsync(u => u.Id == instructorId, ct);
        var who = HomepageService.PublicDisplayName(instructor.Name);
        await _notifications.NotifyUsersAsync(
            [entity.StudentUserId],
            new NotificationDraft(
                request.Accept ? "Tu solicitud de clase particular fue aceptada" : "Tu solicitud de clase particular no fue aceptada",
                request.Accept
                    ? $"{who} aceptó tu solicitud: «{entity.Reply}»"
                    : $"{who} no puede darte la clase por ahora.{(entity.Reply is null ? "" : $" «{entity.Reply}»")}",
                NotificationTypes.System,
                RelatedEntity: "private_class_request",
                RelatedId: entity.Id,
                Link: "/instructores",
                DedupeKey: $"private-class-reply-{entity.Id}"),
            ct);

        return (await MapAsync([entity], ct))[0];
    }

    public async Task CancelAsync(int studentId, int requestId, CancellationToken ct)
    {
        var entity = await _db.Set<PrivateClassRequest>().FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (entity is null || entity.StudentUserId != studentId)
        {
            throw new NotFoundException("Solicitud no encontrada.", "private_class_not_found");
        }

        entity.Cancel(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PrivateClassSettingsDto> GetSettingsAsync(int instructorId, CancellationToken ct)
    {
        var listing = await _db.Set<InstructorListing>().AsNoTracking()
            .FirstOrDefaultAsync(l => l.UserId == instructorId, ct);
        return new PrivateClassSettingsDto(listing is not null, listing?.AcceptsPrivateClasses ?? false);
    }

    public async Task<PrivateClassSettingsDto> SetAcceptsAsync(int instructorId, bool accepts, CancellationToken ct)
    {
        var listing = await _db.Set<InstructorListing>().FirstOrDefaultAsync(l => l.UserId == instructorId, ct);
        if (listing is null)
        {
            if (accepts)
            {
                throw new DomainException(
                    "Primero activa \"Aparecer en el directorio de instructores\" en Perfil > Preferencias.",
                    400,
                    "directory_listing_required");
            }

            return new PrivateClassSettingsDto(false, false);
        }

        listing.SetAcceptsPrivateClasses(accepts);
        await _db.SaveChangesAsync(ct);
        return new PrivateClassSettingsDto(true, accepts);
    }

    private async Task EnsureListedAsync(int instructorId, CancellationToken ct)
    {
        var listed = await _db.Set<InstructorListing>().AsNoTracking().AnyAsync(l => l.UserId == instructorId, ct)
            && await _db.Set<User>().AsNoTracking().AnyAsync(u => u.Id == instructorId && u.IsActive && u.Role == Roles.Teacher, ct);
        if (!listed)
        {
            throw new NotFoundException("Instructor no encontrado.", "instructor_not_found");
        }
    }

    /// <summary>Emails are shared only after the instructor accepts; until then each side sees the other's public name.</summary>
    private async Task<List<PrivateClassRequestDto>> MapAsync(IReadOnlyList<PrivateClassRequest> rows, CancellationToken ct)
    {
        var ids = rows.SelectMany(r => new[] { r.InstructorUserId, r.StudentUserId }).Distinct().ToList();
        var users = await _db.Set<User>().AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Email })
            .ToDictionaryAsync(u => u.Id, ct);

        return rows.Select(r =>
        {
            var accepted = r.Status == PrivateClassStatuses.Accepted;
            var instructor = users.GetValueOrDefault(r.InstructorUserId);
            var student = users.GetValueOrDefault(r.StudentUserId);
            return new PrivateClassRequestDto(
                r.Id,
                r.InstructorUserId,
                instructor is null ? "Instructor" : HomepageService.PublicDisplayName(instructor.Name),
                accepted ? instructor?.Email : null,
                r.StudentUserId,
                student is null ? "Estudiante" : accepted ? student.Name : HomepageService.PublicDisplayName(student.Name),
                accepted ? student?.Email : null,
                r.Category,
                r.PreferredSchedule,
                r.Message,
                r.Status,
                r.Reply,
                r.CreatedAt,
                r.RespondedAt);
        }).ToList();
    }
}

public sealed record InstructorReviewDto(int Id, int Stars, string Comment, string AuthorName, DateTime UpdatedAt, bool Hidden);

public sealed record MyInstructorRatingDto(int Stars, string? Comment);

public sealed record RateInstructorRequest(int Stars, string? Comment);

public sealed record CreatePrivateClassRequest(int InstructorId, string? Category, string? PreferredSchedule, string? Message);

public sealed record RespondPrivateClassRequest(bool Accept, string? Reply);

public sealed record PrivateClassSettingsDto(bool Listed, bool AcceptsPrivateClasses);

public sealed record PrivateClassRequestDto(
    int Id,
    int InstructorId,
    string InstructorName,
    string? InstructorEmail,
    int StudentId,
    string StudentName,
    string? StudentEmail,
    string Category,
    string PreferredSchedule,
    string Message,
    string Status,
    string? Reply,
    DateTime CreatedAt,
    DateTime? RespondedAt);
