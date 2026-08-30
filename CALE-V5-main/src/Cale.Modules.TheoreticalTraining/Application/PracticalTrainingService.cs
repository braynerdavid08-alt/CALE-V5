using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed class PracticalTrainingService
{
    private readonly CaleDbContext _db;
    private readonly IUserStore _users;
    private readonly TheoryTrainingService _theory;
    private readonly IClock _clock;

    public PracticalTrainingService(
        CaleDbContext db,
        IUserStore users,
        TheoryTrainingService theory,
        IClock clock)
    {
        _db = db;
        _users = users;
        _theory = theory;
        _clock = clock;
    }

    public async Task<IReadOnlyList<PracticalVehicleDto>> ListVehiclesAsync(
        int schoolUserId,
        bool activeOnly,
        CancellationToken ct)
    {
        var query = _db.Set<PracticalVehicle>().Where(x => x.SchoolUserId == schoolUserId);
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.Label)
            .Select(x => new PracticalVehicleDto(x.Id, x.Label, x.Plate, x.IsActive))
            .ToListAsync(ct);
    }

    public async Task<PracticalVehicleDto> SaveVehicleAsync(
        int schoolUserId,
        int? id,
        SavePracticalVehicleRequest request,
        CancellationToken ct)
    {
        var now = _clock.UtcNow;
        PracticalVehicle entity;
        if (id is > 0)
        {
            entity = await _db.Set<PracticalVehicle>()
                .FirstOrDefaultAsync(x => x.Id == id && x.SchoolUserId == schoolUserId, ct)
                ?? throw new NotFoundException("Vehículo no encontrado.", "vehicle_not_found");
        }
        else
        {
            entity = new PracticalVehicle { SchoolUserId = schoolUserId, CreatedAt = now };
            await _db.Set<PracticalVehicle>().AddAsync(entity, ct);
        }

        entity.Label = request.Label.Trim();
        entity.Plate = string.IsNullOrWhiteSpace(request.Plate) ? null : request.Plate.Trim().ToUpperInvariant();
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        return new PracticalVehicleDto(entity.Id, entity.Label, entity.Plate, entity.IsActive);
    }

    public async Task<IReadOnlyList<PracticalLessonSessionDto>> ListSchoolLessonsAsync(
        int schoolUserId,
        DateOnly? weekStart,
        CancellationToken ct)
    {
        var start = weekStart ?? StartOfWeek(ColombiaTime.TodayInColombia());
        var end = start.AddDays(6);
        var sessions = await _db.Set<PracticalLessonSession>()
            .Include(x => x.Vehicle)
            .Where(x => x.SchoolUserId == schoolUserId
                && x.SessionDate >= start
                && x.SessionDate <= end
                && x.Status != PracticalLessonStatuses.Cancelled)
            .OrderBy(x => x.SessionDate)
            .ThenBy(x => x.StartTime)
            .ToListAsync(ct);

        var result = new List<PracticalLessonSessionDto>();
        foreach (var s in sessions)
        {
            result.Add(await MapLessonAsync(s, null, ct));
        }

        return result;
    }

    public async Task<PracticalLessonSessionDto> CreateLessonAsync(
        int schoolUserId,
        CreatePracticalLessonRequest request,
        CancellationToken ct)
    {
        if (request.SessionDate.DayOfWeek == DayOfWeek.Sunday)
        {
            throw new DomainException("No se programan clases los domingos.", 400, "sunday_disabled");
        }

        var vehicle = await _db.Set<PracticalVehicle>()
            .FirstOrDefaultAsync(x => x.Id == request.VehicleId && x.SchoolUserId == schoolUserId && x.IsActive, ct)
            ?? throw new DomainException("Vehículo no válido.", 400, "invalid_vehicle");

        var instructor = await _users.GetByIdAsync(request.InstructorUserId, ct)
            ?? throw new DomainException("Instructor no válido.", 400, "invalid_instructor");
        if (instructor.SchoolId != schoolUserId || instructor.Role != Roles.Teacher)
        {
            throw new DomainException("El instructor no pertenece a la escuela.", 400, "invalid_instructor");
        }

        var start = ParseTime(request.StartTime);
        var end = ParseTime(request.EndTime);
        if (end <= start)
        {
            throw new DomainException("La hora de fin debe ser posterior al inicio.", 400, "invalid_time_range");
        }

        var now = _clock.UtcNow;
        var session = new PracticalLessonSession
        {
            SchoolUserId = schoolUserId,
            SessionDate = request.SessionDate,
            StartTime = start,
            EndTime = end,
            InstructorUserId = request.InstructorUserId,
            VehicleId = vehicle.Id,
            Capacity = Math.Clamp(request.Capacity ?? 1, 1, 4),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Status = PracticalLessonStatuses.Scheduled,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _db.Set<PracticalLessonSession>().AddAsync(session, ct);
        await _db.SaveChangesAsync(ct);
        session.Vehicle = vehicle;
        return await MapLessonAsync(session, null, ct);
    }

    public async Task CancelLessonAsync(int schoolUserId, int lessonId, CancellationToken ct)
    {
        var session = await RequireLessonAsync(schoolUserId, lessonId, ct);
        session.Status = PracticalLessonStatuses.Cancelled;
        session.UpdatedAt = _clock.UtcNow;
        var reservations = await _db.Set<PracticalLessonReservation>()
            .Where(x => x.LessonSessionId == lessonId
                && PracticalReservationStatuses.ActiveStatuses.Contains(x.Status))
            .ToListAsync(ct);
        foreach (var r in reservations)
        {
            r.Status = PracticalReservationStatuses.CancelledBySchool;
            r.CancelledAt = _clock.UtcNow;
            r.UpdatedAt = _clock.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PracticalStudentDashboardDto> GetStudentDashboardAsync(
        int studentUserId,
        CancellationToken ct)
    {
        var (schoolUserId, _) = await _theory.ResolveStudentSchoolPublicAsync(studentUserId, ct);
        var eligibility = await _theory.GetPracticalEligibilityAsync(schoolUserId, studentUserId, ct);
        var nowUtc = _clock.UtcNow;
        var today = ColombiaTime.TodayInColombia();

        var myReservations = await _db.Set<PracticalLessonReservation>()
            .Include(x => x.LessonSession)!.ThenInclude(s => s!.Vehicle)
            .Where(x => x.StudentUserId == studentUserId
                && PracticalReservationStatuses.ActiveStatuses.Contains(x.Status))
            .ToListAsync(ct);

        var upcoming = myReservations
            .Where(r => r.LessonSession is not null)
            .Select(r => r.LessonSession!)
            .Where(s => s.Status == PracticalLessonStatuses.Scheduled
                && ColombiaTime.ToUtc(s.SessionDate, s.StartTime) >= nowUtc)
            .OrderBy(s => s.SessionDate)
            .ThenBy(s => s.StartTime)
            .ToList();

        PracticalLessonSessionDto? nextDto = null;
        if (upcoming.Count > 0)
        {
            nextDto = await MapLessonAsync(upcoming[0], studentUserId, ct);
        }

        var upcomingDtos = new List<PracticalLessonSessionDto>();
        foreach (var s in upcoming.Take(8))
        {
            upcomingDtos.Add(await MapLessonAsync(s, studentUserId, ct));
        }

        var available = await _db.Set<PracticalLessonSession>()
            .Include(x => x.Vehicle)
            .Where(x => x.SchoolUserId == schoolUserId
                && x.Status == PracticalLessonStatuses.Scheduled
                && x.SessionDate >= today)
            .OrderBy(x => x.SessionDate)
            .ThenBy(x => x.StartTime)
            .Take(30)
            .ToListAsync(ct);

        var availableDtos = new List<PracticalLessonSessionDto>();
        foreach (var s in available)
        {
            availableDtos.Add(await MapLessonAsync(s, studentUserId, ct));
        }

        return new PracticalStudentDashboardDto(eligibility, nextDto, upcomingDtos, availableDtos);
    }

    public async Task<PracticalLessonSessionDto> ReserveAsync(
        int studentUserId,
        int lessonId,
        CancellationToken ct)
    {
        var (schoolUserId, _) = await _theory.ResolveStudentSchoolPublicAsync(studentUserId, ct);
        var eligibility = await _theory.GetPracticalEligibilityAsync(schoolUserId, studentUserId, ct);
        if (!eligibility.CanBookPractical)
        {
            throw new DomainException(
                eligibility.BlockReason ?? "Aún no cumples los requisitos para clases de manejo.",
                400,
                "practical_not_eligible");
        }

        var session = await _db.Set<PracticalLessonSession>()
            .Include(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.Id == lessonId && x.SchoolUserId == schoolUserId, ct)
            ?? throw new NotFoundException("Clase no encontrada.", "lesson_not_found");

        if (session.Status != PracticalLessonStatuses.Scheduled)
        {
            throw new DomainException("Esta clase no está disponible.", 400, "lesson_unavailable");
        }

        var reserved = await _db.Set<PracticalLessonReservation>()
            .CountAsync(x => x.LessonSessionId == lessonId
                && PracticalReservationStatuses.ActiveStatuses.Contains(x.Status), ct);
        if (reserved >= session.Capacity)
        {
            throw new DomainException("No hay cupos disponibles.", 400, "lesson_full");
        }

        var existing = await _db.Set<PracticalLessonReservation>()
            .FirstOrDefaultAsync(x => x.LessonSessionId == lessonId
                && x.StudentUserId == studentUserId
                && PracticalReservationStatuses.ActiveStatuses.Contains(x.Status), ct);
        if (existing is not null)
        {
            return await MapLessonAsync(session, studentUserId, ct);
        }

        var now = _clock.UtcNow;
        await _db.Set<PracticalLessonReservation>().AddAsync(new PracticalLessonReservation
        {
            LessonSessionId = lessonId,
            StudentUserId = studentUserId,
            Status = PracticalReservationStatuses.Reserved,
            ReservedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        }, ct);
        await _db.SaveChangesAsync(ct);
        return await MapLessonAsync(session, studentUserId, ct);
    }

    public async Task CancelReservationAsync(int studentUserId, int reservationId, CancellationToken ct)
    {
        var reservation = await _db.Set<PracticalLessonReservation>()
            .FirstOrDefaultAsync(x => x.Id == reservationId && x.StudentUserId == studentUserId, ct)
            ?? throw new NotFoundException("Reserva no encontrada.", "reservation_not_found");

        if (!PracticalReservationStatuses.ActiveStatuses.Contains(reservation.Status))
        {
            return;
        }

        reservation.Status = PracticalReservationStatuses.CancelledByStudent;
        reservation.CancelledAt = _clock.UtcNow;
        reservation.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<PracticalLessonSession> RequireLessonAsync(
        int schoolUserId,
        int lessonId,
        CancellationToken ct) =>
        await _db.Set<PracticalLessonSession>()
            .FirstOrDefaultAsync(x => x.Id == lessonId && x.SchoolUserId == schoolUserId, ct)
            ?? throw new NotFoundException("Clase no encontrada.", "lesson_not_found");

    private async Task<PracticalLessonSessionDto> MapLessonAsync(
        PracticalLessonSession session,
        int? studentUserId,
        CancellationToken ct)
    {
        var instructor = await _users.GetByIdAsync(session.InstructorUserId, ct);
        var reserved = await _db.Set<PracticalLessonReservation>()
            .CountAsync(x => x.LessonSessionId == session.Id
                && PracticalReservationStatuses.ActiveStatuses.Contains(x.Status), ct);

        int? myReservationId = null;
        string? bookingState = null;
        string? bookingMessage = null;

        if (studentUserId is int sid)
        {
            var mine = await _db.Set<PracticalLessonReservation>()
                .FirstOrDefaultAsync(x => x.LessonSessionId == session.Id
                    && x.StudentUserId == sid
                    && PracticalReservationStatuses.ActiveStatuses.Contains(x.Status), ct);
            if (mine is not null)
            {
                myReservationId = mine.Id;
                bookingState = "reserved";
                bookingMessage = "Reservada";
            }
            else if (reserved >= session.Capacity)
            {
                bookingState = "full";
                bookingMessage = "Sin cupos";
            }
            else
            {
                bookingState = "can_reserve";
                bookingMessage = $"{session.Capacity - reserved} cupo(s)";
            }
        }

        return new PracticalLessonSessionDto(
            session.Id,
            session.SessionDate.ToString("yyyy-MM-dd"),
            session.StartTime.ToString("HH:mm"),
            session.EndTime.ToString("HH:mm"),
            session.InstructorUserId,
            instructor?.Name ?? "Instructor",
            session.VehicleId,
            session.Vehicle?.Label ?? "Vehículo",
            session.Capacity,
            reserved,
            Math.Max(0, session.Capacity - reserved),
            session.Status,
            session.Notes,
            bookingState,
            bookingMessage,
            myReservationId);
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }

    private static TimeOnly ParseTime(string value)
    {
        if (TimeOnly.TryParse(value.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var time))
        {
            return time;
        }

        throw new DomainException("La hora no es válida.", 400, "invalid_time");
    }
}
