using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed partial class TheoryExamScheduleService
{
    private static readonly string[] DayNames =
        ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    private static ExamTemplateDto MapTemplate(TheoryExamScheduleTemplate t) =>
        new(t.Id, t.DayOfWeek, T(t.StartTime), t.Capacity, t.IsActive);

    public async Task<IReadOnlyList<ExamTemplateDto>> ListTemplatesAsync(int schoolUserId, CancellationToken ct)
    {
        var rows = await _db.Set<TheoryExamScheduleTemplate>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId)
            .ToListAsync(ct);
        return rows
            .OrderBy(x => (x.DayOfWeek + 6) % 7)
            .ThenBy(x => x.StartTime)
            .Select(MapTemplate)
            .ToList();
    }

    /// <summary>Creates the hour on each day, or reactivates/updates it if it already exists (never duplicates).</summary>
    public async Task<IReadOnlyList<ExamTemplateDto>> CreateTemplatesAsync(
        int schoolUserId,
        int actorUserId,
        CreateExamTemplatesRequest request,
        CancellationToken ct)
    {
        await _membership.EnsureActiveAsync(schoolUserId, ct);
        var time = ParseSlotTime(request.Time);
        var capacity = ValidateCapacity(request.Capacity);
        var days = (request.DaysOfWeek ?? [])
            .Distinct()
            .ToList();
        if (days.Count == 0 || days.Any(d => d is < 0 or > 6))
        {
            throw new DomainException("Elige al menos un día de la semana.", 400, "days_required");
        }

        var now = _clock.UtcNow;
        var existing = await _db.Set<TheoryExamScheduleTemplate>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.StartTime == time
                && days.Contains(x.DayOfWeek))
            .ToListAsync(ct);

        var result = new List<TheoryExamScheduleTemplate>();
        foreach (var day in days)
        {
            var template = existing.FirstOrDefault(x => x.DayOfWeek == day);
            if (template is null)
            {
                template = new TheoryExamScheduleTemplate
                {
                    SchoolUserId = schoolUserId,
                    DayOfWeek = day,
                    StartTime = time,
                    Capacity = capacity,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.Set<TheoryExamScheduleTemplate>().Add(template);
                SchoolAudit.Add(_db, now, schoolUserId, actorUserId, SchoolAuditAreas.ExamSchedule,
                    "template_created",
                    $"Nuevo horario de examen: {DayNames[day]} {T(time)} ({capacity} cupo(s)).",
                    newValue: $"{DayNames[day]} {T(time)} · {capacity}");
            }
            else if (!template.IsActive || template.Capacity != capacity)
            {
                var old = $"{(template.IsActive ? "activo" : "inactivo")} · {template.Capacity}";
                template.IsActive = true;
                template.Capacity = capacity;
                template.UpdatedAt = now;
                SchoolAudit.Add(_db, now, schoolUserId, actorUserId, SchoolAuditAreas.ExamSchedule,
                    "template_updated",
                    $"Horario {DayNames[day]} {T(time)} activado con {capacity} cupo(s).",
                    entityType: "exam_template", entityId: template.Id,
                    oldValue: old, newValue: $"activo · {capacity}");
            }

            result.Add(template);
        }

        await SaveTemplatesAsync(ct);
        return result.OrderBy(x => (x.DayOfWeek + 6) % 7).Select(MapTemplate).ToList();
    }

    public async Task<ExamTemplateDto> UpdateTemplateAsync(
        int schoolUserId,
        int actorUserId,
        int templateId,
        UpdateExamTemplateRequest request,
        CancellationToken ct)
    {
        await _membership.EnsureActiveAsync(schoolUserId, ct);
        var template = await _db.Set<TheoryExamScheduleTemplate>()
            .FirstOrDefaultAsync(x => x.Id == templateId && x.SchoolUserId == schoolUserId, ct)
            ?? throw new NotFoundException("Horario no encontrado.", "exam_template_not_found");

        var time = ParseSlotTime(request.Time);
        var capacity = ValidateCapacity(request.Capacity);
        if (time != template.StartTime)
        {
            var clash = await _db.Set<TheoryExamScheduleTemplate>()
                .AnyAsync(x => x.SchoolUserId == schoolUserId
                    && x.DayOfWeek == template.DayOfWeek
                    && x.StartTime == time
                    && x.Id != template.Id, ct);
            if (clash)
            {
                throw new DomainException(
                    $"Ya tienes un horario el {DayNames[template.DayOfWeek]} a las {T(time)}.",
                    409,
                    "exam_template_duplicate");
            }
        }

        var old = $"{T(template.StartTime)} · {template.Capacity} · {(template.IsActive ? "activo" : "inactivo")}";
        var now = _clock.UtcNow;
        template.StartTime = time;
        template.Capacity = capacity;
        template.IsActive = request.IsActive;
        template.UpdatedAt = now;
        var updated = $"{T(time)} · {capacity} · {(request.IsActive ? "activo" : "inactivo")}";
        if (old != updated)
        {
            SchoolAudit.Add(_db, now, schoolUserId, actorUserId, SchoolAuditAreas.ExamSchedule,
                "template_updated",
                $"Horario del {DayNames[template.DayOfWeek]} modificado.",
                entityType: "exam_template", entityId: template.Id,
                oldValue: old, newValue: updated);
        }

        await SaveTemplatesAsync(ct);
        return MapTemplate(template);
    }

    /// <summary>Removes the recurring hour. Existing bookings are kept (they show as a closed hour).</summary>
    public async Task DeleteTemplateAsync(int schoolUserId, int actorUserId, int templateId, CancellationToken ct)
    {
        await _membership.EnsureActiveAsync(schoolUserId, ct);
        var template = await _db.Set<TheoryExamScheduleTemplate>()
            .FirstOrDefaultAsync(x => x.Id == templateId && x.SchoolUserId == schoolUserId, ct)
            ?? throw new NotFoundException("Horario no encontrado.", "exam_template_not_found");

        _db.Remove(template);
        SchoolAudit.Add(_db, _clock.UtcNow, schoolUserId, actorUserId, SchoolAuditAreas.ExamSchedule,
            "template_deleted",
            $"Horario eliminado: {DayNames[template.DayOfWeek]} {T(template.StartTime)}.",
            entityType: "exam_template", entityId: template.Id,
            oldValue: $"{DayNames[template.DayOfWeek]} {T(template.StartTime)} · {template.Capacity}");
        await _db.SaveChangesAsync(ct);
    }

    private async Task SaveTemplatesAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _db.ChangeTracker.Clear();
            throw new DomainException(
                "Ese horario ya existe para ese día.",
                409,
                "exam_template_duplicate");
        }
    }
}
