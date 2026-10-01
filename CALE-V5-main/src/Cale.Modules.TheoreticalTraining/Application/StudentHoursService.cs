using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Domain;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.TheoreticalTraining.Application;

/// <summary>Theory/workshop hours of a student, with manual corrections that feed the existing progress rules.</summary>
public sealed class StudentHoursService
{
    public const decimal MaxHours = 500m;

    private readonly CaleDbContext _db;
    private readonly IClock _clock;
    private readonly ISchoolMembershipGuard _membership;
    private readonly INotificationPublisher _notifications;

    public StudentHoursService(
        CaleDbContext db,
        IClock clock,
        ISchoolMembershipGuard membership,
        INotificationPublisher notifications)
    {
        _db = db;
        _clock = clock;
        _membership = membership;
        _notifications = notifications;
    }

    public async Task<StudentHoursDto> GetAsync(int schoolUserId, int studentUserId, CancellationToken ct)
    {
        var student = await LoadStudentAsync(schoolUserId, studentUserId, ct);
        var settings = await _db.Set<TheoryTrainingSettings>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId, ct)
            ?? new TheoryTrainingSettings { SchoolUserId = schoolUserId };
        var license = await _db.Set<SchoolStudentEnrollment>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId)
            .Select(x => x.LicenseCategories)
            .FirstOrDefaultAsync(ct);
        var (theoryRequired, workshopRequired) = LicenseCategoryPolicyHelper.ResolveHourRequirements(settings, license);
        var hours = await TrainingHoursCalculator.ComputeAsync(_db, schoolUserId, studentUserId, ct);

        var history = await _db.Set<TrainingHoursAdjustment>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(50)
            .ToListAsync(ct);
        var actorIds = history.Where(x => x.PerformedByUserId is int).Select(x => x.PerformedByUserId!.Value).Distinct().ToList();
        var actors = actorIds.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Set<User>()
                .AsNoTracking()
                .Where(u => actorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return new StudentHoursDto(
            studentUserId,
            student.Name,
            Line(theoryRequired, hours.TheoryAttended, hours.TheoryAdjusted, hours.TheoryTotal),
            Line(workshopRequired, hours.WorkshopAttended, hours.WorkshopAdjusted, hours.WorkshopTotal),
            history.Select(x => new HoursAdjustmentDto(
                x.Id,
                x.Category,
                x.PreviousHours,
                x.NewHours,
                x.DeltaHours,
                x.Reason,
                x.PerformedByUserId is int a && actors.TryGetValue(a, out var name) ? name : null,
                x.CreatedAt)).ToList());
    }

    public async Task<StudentHoursDto> AdjustAsync(
        int schoolUserId,
        int actorUserId,
        int studentUserId,
        AdjustStudentHoursRequest request,
        CancellationToken ct)
    {
        await _membership.EnsureActiveAsync(schoolUserId, ct);
        var student = await LoadStudentAsync(schoolUserId, studentUserId, ct);
        var category = request.Category?.Trim() switch
        {
            var c when string.Equals(c, TheoryTopicCategories.Theory, StringComparison.OrdinalIgnoreCase) => TheoryTopicCategories.Theory,
            var c when string.Equals(c, TheoryTopicCategories.Workshop, StringComparison.OrdinalIgnoreCase) => TheoryTopicCategories.Workshop,
            _ => throw new DomainException("Elige si son horas de teoría o de taller.", 400, "hours_invalid")
        };
        var reason = request.Reason?.Trim() ?? "";
        if (reason.Length < 5)
        {
            throw new DomainException("Escribe el motivo del cambio (mínimo 5 letras).", 400, "hours_reason_required");
        }

        if (reason.Length > 300)
        {
            reason = reason[..300];
        }

        var newHours = Math.Round(request.NewHours, 1, MidpointRounding.AwayFromZero);
        if (newHours < 0 || newHours > MaxHours)
        {
            throw new DomainException($"Las horas deben estar entre 0 y {MaxHours:0}.", 400, "hours_invalid");
        }

        var hours = await TrainingHoursCalculator.ComputeAsync(_db, schoolUserId, studentUserId, ct);
        var current = category == TheoryTopicCategories.Theory ? hours.TheoryTotal : hours.WorkshopTotal;
        var delta = newHours - current;
        if (delta == 0)
        {
            throw new DomainException("El nuevo valor es igual al actual.", 400, "hours_unchanged");
        }

        var now = _clock.UtcNow;
        var label = category == TheoryTopicCategories.Theory ? "teoría" : "taller";
        var adjustment = new TrainingHoursAdjustment
        {
            SchoolUserId = schoolUserId,
            StudentUserId = studentUserId,
            Category = category,
            DeltaHours = delta,
            PreviousHours = current,
            NewHours = newHours,
            Reason = reason,
            PerformedByUserId = actorUserId,
            CreatedAt = now
        };
        _db.Set<TrainingHoursAdjustment>().Add(adjustment);
        SchoolAudit.Add(_db, now, schoolUserId, actorUserId, SchoolAuditAreas.StudentHours,
            category == TheoryTopicCategories.Theory ? "theory_hours_adjusted" : "workshop_hours_adjusted",
            $"Horas de {label} de {student.Name}: {current:0.#} → {newHours:0.#}.",
            entityType: "student", entityId: studentUserId, studentUserId: studentUserId,
            oldValue: current.ToString("0.#"), newValue: newHours.ToString("0.#"), reason: reason);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _notifications.NotifyUsersAsync(
                [studentUserId],
                new NotificationDraft(
                    "Tus horas fueron actualizadas",
                    $"Tu escuela registró {newHours:0.#} h de {label}.",
                    NotificationTypes.TheoryClass,
                    RelatedEntity: "training_hours",
                    RelatedId: adjustment.Id,
                    Link: "/student/training"),
                ct);
        }
        catch (Exception)
        {
            // Best effort; the change is saved and audited.
        }

        return await GetAsync(schoolUserId, studentUserId, ct);
    }

    private static HoursLineDto Line(int required, decimal attended, decimal adjusted, decimal total) =>
        new(required, attended, adjusted, total, Math.Max(0, required - total));

    /// <summary>The student must belong to this school; another school's student looks like "not found".</summary>
    private async Task<User> LoadStudentAsync(int schoolUserId, int studentUserId, CancellationToken ct)
    {
        var user = await _db.Set<User>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == studentUserId, ct);
        var belongs = user is not null
            && user.Role == Roles.Student
            && (user.SchoolId == schoolUserId
                || await _db.Set<SchoolStudentEnrollment>()
                    .AnyAsync(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId, ct)
                || await _db.Set<SchoolApprenticeProfile>()
                    .AnyAsync(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId, ct));
        return belongs ? user! : throw new NotFoundException("Estudiante no encontrado.", "student_not_found");
    }
}

public sealed class SchoolAuditService
{
    private readonly CaleDbContext _db;

    public SchoolAuditService(CaleDbContext db) => _db = db;

    public async Task<IReadOnlyList<SchoolAuditEntryDto>> ListAsync(
        int schoolUserId,
        int? studentUserId,
        string? area,
        int take,
        CancellationToken ct)
    {
        var query = _db.Set<SchoolAuditEntry>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId);
        if (studentUserId is int sid)
        {
            query = query.Where(x => x.StudentUserId == sid);
        }

        if (!string.IsNullOrWhiteSpace(area))
        {
            query = query.Where(x => x.Area == area);
        }

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);
        var ids = rows.Select(x => x.ActorUserId)
            .Concat(rows.Select(x => x.StudentUserId))
            .Where(x => x is int)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();
        var names = ids.Count == 0
            ? new Dictionary<int, string>()
            : await _db.Set<User>()
                .AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return rows.Select(x => new SchoolAuditEntryDto(
            x.Id,
            x.Area,
            x.Action,
            x.Summary,
            x.OldValue,
            x.NewValue,
            x.Reason,
            x.StudentUserId,
            x.StudentUserId is int s && names.TryGetValue(s, out var sn) ? sn : null,
            x.ActorUserId is int a && names.TryGetValue(a, out var an) ? an : null,
            x.CreatedAt)).ToList();
    }
}
