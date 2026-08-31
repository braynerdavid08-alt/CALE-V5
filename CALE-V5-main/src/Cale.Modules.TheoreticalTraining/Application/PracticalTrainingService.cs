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
        int? instructorUserId,
        int? vehicleId,
        CancellationToken ct)
    {
        var start = weekStart ?? StartOfWeek(ColombiaTime.TodayInColombia());
        var end = start.AddDays(6);
        var query = _db.Set<PracticalLessonSession>()
            .Include(x => x.Vehicle)
            .Where(x => x.SchoolUserId == schoolUserId
                && x.SessionDate >= start
                && x.SessionDate <= end
                && x.Status != PracticalLessonStatuses.Cancelled);

        if (instructorUserId is > 0)
        {
            query = query.Where(x => x.InstructorUserId == instructorUserId);
        }

        if (vehicleId is > 0)
        {
            query = query.Where(x => x.VehicleId == vehicleId);
        }

        var sessions = await query
            .OrderBy(x => x.SessionDate)
            .ThenBy(x => x.StartTime)
            .ToListAsync(ct);

        var result = new List<PracticalLessonSessionDto>();
        foreach (var s in sessions)
        {
            result.Add(await MapLessonAsync(s, null, includeAssignment: true, ct));
        }

        return result;
    }

    public async Task<IReadOnlyList<PracticalSchedulingStudentDto>> ListSchedulingStudentsAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var enrollments = await _db.Set<SchoolStudentEnrollment>()
            .Where(x => x.SchoolUserId == schoolUserId
                && StudentEnrollmentStatuses.CanReserveStatuses.Contains(x.Status))
            .OrderBy(x => x.StudentUserId)
            .ToListAsync(ct);

        var result = new List<PracticalSchedulingStudentDto>();
        foreach (var enrollment in enrollments)
        {
            var user = await _users.GetByIdAsync(enrollment.StudentUserId, ct);
            var eligibility = await _theory.GetPracticalEligibilityAsync(
                schoolUserId,
                enrollment.StudentUserId,
                ct);
            var progress = await GetStudentLessonProgressAsync(
                schoolUserId,
                enrollment.StudentUserId,
                enrollment.LicenseCategories,
                ct);

            result.Add(new PracticalSchedulingStudentDto(
                enrollment.StudentUserId,
                user?.Name ?? $"Estudiante {enrollment.StudentUserId}",
                enrollment.LicenseCategories,
                progress.Completed,
                progress.Required,
                progress.NextNumber,
                eligibility.CanBookPractical,
                eligibility.BlockReason));
        }

        return result.OrderBy(x => x.StudentName).ToList();
    }

    public async Task<PracticalLessonSessionDto> QuickAssignAsync(
        int schoolUserId,
        QuickAssignPracticalRequest request,
        CancellationToken ct)
    {
        if (request.SessionDate.DayOfWeek == DayOfWeek.Sunday)
        {
            throw new DomainException("No se programan clases los domingos.", 400, "sunday_disabled");
        }

        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == request.StudentUserId, ct)
            ?? throw new DomainException("El estudiante no está inscrito en la escuela.", 400, "student_not_enrolled");

        if (!StudentEnrollmentStatuses.CanReserve.Contains(enrollment.Status))
        {
            throw new DomainException(
                "El estudiante debe estar autorizado para asignar clases.",
                400,
                "student_not_authorized");
        }

        var start = ParseTime(request.StartTime);
        var end = ParseTime(request.EndTime);
        if (end <= start)
        {
            throw new DomainException("La hora de fin debe ser posterior al inicio.", 400, "invalid_time_range");
        }

        var session = await _db.Set<PracticalLessonSession>()
            .Include(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.SessionDate == request.SessionDate
                && x.StartTime == start
                && x.InstructorUserId == request.InstructorUserId
                && x.VehicleId == request.VehicleId
                && x.Status != PracticalLessonStatuses.Cancelled, ct);

        var now = _clock.UtcNow;
        if (session is null)
        {
            session = await CreateLessonInternalAsync(
                schoolUserId,
                request.SessionDate,
                start,
                end,
                request.InstructorUserId,
                request.VehicleId,
                capacity: 1,
                notes: null,
                ct);
        }
        else
        {
            await EnsureNoScheduleConflictAsync(
                schoolUserId,
                request.SessionDate,
                start,
                end,
                request.InstructorUserId,
                request.VehicleId,
                session.Id,
                ct);
        }

        var existingReservation = await _db.Set<PracticalLessonReservation>()
            .FirstOrDefaultAsync(x => x.LessonSessionId == session.Id
                && PracticalReservationStatuses.OccupiesSeatStatuses.Contains(x.Status), ct);

        if (existingReservation is not null)
        {
            if (existingReservation.StudentUserId == request.StudentUserId)
            {
                return await MapLessonAsync(session, null, includeAssignment: true, ct);
            }

            throw new DomainException(
                "Ese horario ya tiene un estudiante asignado.",
                400,
                "slot_taken");
        }

        var studentConflict = await _db.Set<PracticalLessonReservation>()
            .Include(x => x.LessonSession)
            .AnyAsync(x => x.StudentUserId == request.StudentUserId
                && x.LessonSession!.SchoolUserId == schoolUserId
                && x.LessonSession.SessionDate == request.SessionDate
                && PracticalReservationStatuses.OccupiesSeatStatuses.Contains(x.Status), ct);
        if (studentConflict)
        {
            throw new DomainException(
                "El estudiante ya tiene una clase ese día.",
                400,
                "student_day_taken");
        }

        await _db.Set<PracticalLessonReservation>().AddAsync(new PracticalLessonReservation
        {
            LessonSessionId = session.Id,
            StudentUserId = request.StudentUserId,
            Status = PracticalReservationStatuses.Reserved,
            ReservedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        }, ct);
        await _db.SaveChangesAsync(ct);

        return await MapLessonAsync(session, null, includeAssignment: true, ct);
    }

    public async Task UnassignStudentAsync(int schoolUserId, int lessonId, CancellationToken ct)
    {
        await RequireLessonAsync(schoolUserId, lessonId, ct);
        var reservations = await _db.Set<PracticalLessonReservation>()
            .Where(x => x.LessonSessionId == lessonId
                && PracticalReservationStatuses.OccupiesSeatStatuses.Contains(x.Status))
            .ToListAsync(ct);

        var now = _clock.UtcNow;
        foreach (var reservation in reservations)
        {
            reservation.Status = PracticalReservationStatuses.CancelledBySchool;
            reservation.CancelledAt = now;
            reservation.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> DuplicatePreviousWeekAsync(
        int schoolUserId,
        DuplicatePracticalWeekRequest request,
        CancellationToken ct)
    {
        var targetStart = StartOfWeek(request.WeekStart);
        var sourceStart = targetStart.AddDays(-7);
        var sourceEnd = sourceStart.AddDays(6);

        var sourceSessions = await _db.Set<PracticalLessonSession>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.InstructorUserId == request.InstructorUserId
                && x.VehicleId == request.VehicleId
                && x.SessionDate >= sourceStart
                && x.SessionDate <= sourceEnd
                && x.Status != PracticalLessonStatuses.Cancelled)
            .OrderBy(x => x.SessionDate)
            .ThenBy(x => x.StartTime)
            .ToListAsync(ct);

        if (sourceSessions.Count == 0)
        {
            return 0;
        }

        var created = 0;
        foreach (var source in sourceSessions)
        {
            var targetDate = source.SessionDate.AddDays(7);
            if (targetDate.DayOfWeek == DayOfWeek.Sunday)
            {
                continue;
            }

            var exists = await _db.Set<PracticalLessonSession>()
                .AnyAsync(x => x.SchoolUserId == schoolUserId
                    && x.SessionDate == targetDate
                    && x.StartTime == source.StartTime
                    && x.InstructorUserId == request.InstructorUserId
                    && x.VehicleId == request.VehicleId
                    && x.Status != PracticalLessonStatuses.Cancelled, ct);
            if (exists)
            {
                continue;
            }

            await CreateLessonInternalAsync(
                schoolUserId,
                targetDate,
                source.StartTime,
                source.EndTime,
                request.InstructorUserId,
                request.VehicleId,
                source.Capacity,
                source.Notes,
                ct);
            created++;
        }

        return created;
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

        var start = ParseTime(request.StartTime);
        var end = ParseTime(request.EndTime);
        if (end <= start)
        {
            throw new DomainException("La hora de fin debe ser posterior al inicio.", 400, "invalid_time_range");
        }

        var session = await CreateLessonInternalAsync(
            schoolUserId,
            request.SessionDate,
            start,
            end,
            request.InstructorUserId,
            request.VehicleId,
            Math.Clamp(request.Capacity ?? 1, 1, 4),
            string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            ct);

        return await MapLessonAsync(session, null, includeAssignment: true, ct);
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
            nextDto = await MapLessonAsync(upcoming[0], studentUserId, includeAssignment: false, ct);
        }

        var upcomingDtos = new List<PracticalLessonSessionDto>();
        foreach (var s in upcoming.Take(8))
        {
            upcomingDtos.Add(await MapLessonAsync(s, studentUserId, includeAssignment: false, ct));
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
            availableDtos.Add(await MapLessonAsync(s, studentUserId, includeAssignment: false, ct));
        }

        var instructorOptions = availableDtos
            .Where(x => x.BookingState is "can_reserve" or "reserved")
            .GroupBy(x => x.InstructorUserId)
            .Select(g => new PracticalInstructorOptionDto(
                g.Key,
                g.First().InstructorName,
                g.Count(x => x.BookingState == "can_reserve")))
            .OrderBy(x => x.InstructorName)
            .ToList();

        return new PracticalStudentDashboardDto(
            eligibility,
            nextDto,
            upcomingDtos,
            availableDtos,
            instructorOptions);
    }

    public async Task<ApprenticePracticalSummaryDto> GetApprenticePracticalSummaryAsync(
        int schoolUserId,
        int studentUserId,
        CancellationToken ct)
    {
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId, ct);
        var (completed, required, _) = await GetStudentLessonProgressAsync(
            schoolUserId,
            studentUserId,
            enrollment?.LicenseCategories,
            ct);

        var today = ColombiaTime.TodayInColombia();
        var reservations = await _db.Set<PracticalLessonReservation>()
            .Include(x => x.LessonSession)
            .Where(x => x.StudentUserId == studentUserId
                && x.LessonSession!.SchoolUserId == schoolUserId
                && PracticalReservationStatuses.OccupiesSeatStatuses.Contains(x.Status))
            .ToListAsync(ct);

        var scheduled = reservations.Count(x =>
            x.Status == PracticalReservationStatuses.Reserved
            && x.LessonSession is not null
            && x.LessonSession.SessionDate >= today);

        var next = reservations
            .Where(x => x.Status == PracticalReservationStatuses.Reserved
                && x.LessonSession is not null
                && x.LessonSession.SessionDate >= today)
            .OrderBy(x => x.LessonSession!.SessionDate)
            .ThenBy(x => x.LessonSession!.StartTime)
            .FirstOrDefault();

        return new ApprenticePracticalSummaryDto(
            completed,
            required,
            scheduled,
            next?.LessonSession?.SessionDate.ToString("yyyy-MM-dd"),
            next?.LessonSession?.StartTime.ToString("HH:mm"));
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
            return await MapLessonAsync(session, studentUserId, includeAssignment: false, ct);
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
        return await MapLessonAsync(session, studentUserId, includeAssignment: false, ct);
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

    public async Task<IReadOnlyList<PracticalLessonSessionDto>> ListAttendanceLessonsAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var today = ColombiaTime.TodayInColombia();
        var from = today.AddDays(-7);
        var to = today.AddDays(14);
        var lessonIds = await _db.Set<PracticalLessonReservation>()
            .Where(r => PracticalReservationStatuses.OccupiesSeatStatuses.Contains(r.Status))
            .Select(r => r.LessonSessionId)
            .Distinct()
            .ToListAsync(ct);

        var sessions = await _db.Set<PracticalLessonSession>()
            .Include(x => x.Vehicle)
            .Where(x => x.SchoolUserId == schoolUserId
                && lessonIds.Contains(x.Id)
                && x.SessionDate >= from
                && x.SessionDate <= to
                && x.Status == PracticalLessonStatuses.Scheduled)
            .OrderBy(x => x.SessionDate)
            .ThenBy(x => x.StartTime)
            .ToListAsync(ct);

        var result = new List<PracticalLessonSessionDto>();
        foreach (var s in sessions)
        {
            result.Add(await MapLessonAsync(s, null, includeAssignment: true, ct));
        }

        return result;
    }

    public async Task<IReadOnlyList<PracticalAttendanceRowDto>> ListAttendanceAsync(
        int schoolUserId,
        int lessonId,
        CancellationToken ct)
    {
        await RequireLessonAsync(schoolUserId, lessonId, ct);
        var reservations = await _db.Set<PracticalLessonReservation>()
            .Where(x => x.LessonSessionId == lessonId
                && PracticalReservationStatuses.OccupiesSeatStatuses.Contains(x.Status))
            .ToListAsync(ct);

        var rows = new List<PracticalAttendanceRowDto>();
        foreach (var r in reservations)
        {
            var user = await _users.GetByIdAsync(r.StudentUserId, ct);
            var status = r.Status switch
            {
                PracticalReservationStatuses.Attended => TheoryAttendanceStatuses.Present,
                PracticalReservationStatuses.NoShow => TheoryAttendanceStatuses.Absent,
                _ => TheoryAttendanceStatuses.Pending
            };
            rows.Add(new PracticalAttendanceRowDto(
                r.StudentUserId,
                user?.Name ?? $"Estudiante {r.StudentUserId}",
                status,
                r.Id));
        }

        return rows.OrderBy(x => x.StudentName).ToList();
    }

    public async Task MarkAttendanceAsync(
        int schoolUserId,
        int lessonId,
        MarkAttendanceRequest request,
        CancellationToken ct)
    {
        await RequireLessonAsync(schoolUserId, lessonId, ct);
        var reservation = await _db.Set<PracticalLessonReservation>()
            .FirstOrDefaultAsync(x => x.LessonSessionId == lessonId
                && x.StudentUserId == request.StudentUserId
                && PracticalReservationStatuses.OccupiesSeatStatuses.Contains(x.Status), ct)
            ?? throw new NotFoundException("Reserva no encontrada.", "reservation_not_found");

        var now = _clock.UtcNow;
        reservation.Status = request.Status switch
        {
            TheoryAttendanceStatuses.Present or TheoryAttendanceStatuses.Late
                => PracticalReservationStatuses.Attended,
            TheoryAttendanceStatuses.Absent => PracticalReservationStatuses.NoShow,
            _ => PracticalReservationStatuses.Reserved
        };
        reservation.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkAttendanceBatchAsync(
        int schoolUserId,
        int lessonId,
        MarkAttendanceBatchRequest request,
        CancellationToken ct)
    {
        foreach (var row in request.Rows)
        {
            await MarkAttendanceAsync(schoolUserId, lessonId, row, ct);
        }
    }

    private async Task<PracticalLessonSession> RequireLessonAsync(
        int schoolUserId,
        int lessonId,
        CancellationToken ct) =>
        await _db.Set<PracticalLessonSession>()
            .FirstOrDefaultAsync(x => x.Id == lessonId && x.SchoolUserId == schoolUserId, ct)
            ?? throw new NotFoundException("Clase no encontrada.", "lesson_not_found");

    private async Task<PracticalLessonSession> CreateLessonInternalAsync(
        int schoolUserId,
        DateOnly sessionDate,
        TimeOnly start,
        TimeOnly end,
        int instructorUserId,
        int vehicleId,
        int capacity,
        string? notes,
        CancellationToken ct)
    {
        var vehicle = await _db.Set<PracticalVehicle>()
            .FirstOrDefaultAsync(x => x.Id == vehicleId && x.SchoolUserId == schoolUserId && x.IsActive, ct)
            ?? throw new DomainException("Vehículo no válido.", 400, "invalid_vehicle");

        var instructor = await _users.GetByIdAsync(instructorUserId, ct)
            ?? throw new DomainException("Instructor no válido.", 400, "invalid_instructor");
        if (instructor.SchoolId != schoolUserId || instructor.Role != Roles.Teacher)
        {
            throw new DomainException("El instructor no pertenece a la escuela.", 400, "invalid_instructor");
        }

        await EnsureNoScheduleConflictAsync(
            schoolUserId,
            sessionDate,
            start,
            end,
            instructorUserId,
            vehicleId,
            excludeLessonId: null,
            ct);

        await EnsureInstructorDailyHourLimitAsync(
            schoolUserId,
            sessionDate,
            start,
            end,
            instructorUserId,
            excludeLessonId: null,
            ct);

        var now = _clock.UtcNow;
        var session = new PracticalLessonSession
        {
            SchoolUserId = schoolUserId,
            SessionDate = sessionDate,
            StartTime = start,
            EndTime = end,
            InstructorUserId = instructorUserId,
            VehicleId = vehicle.Id,
            Capacity = Math.Clamp(capacity, 1, 4),
            Notes = notes,
            Status = PracticalLessonStatuses.Scheduled,
            CreatedAt = now,
            UpdatedAt = now,
            Vehicle = vehicle
        };
        await _db.Set<PracticalLessonSession>().AddAsync(session, ct);
        await _db.SaveChangesAsync(ct);
        return session;
    }

    private async Task EnsureNoScheduleConflictAsync(
        int schoolUserId,
        DateOnly sessionDate,
        TimeOnly start,
        TimeOnly end,
        int instructorUserId,
        int vehicleId,
        int? excludeLessonId,
        CancellationToken ct)
    {
        var query = _db.Set<PracticalLessonSession>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.SessionDate == sessionDate
                && x.Status != PracticalLessonStatuses.Cancelled
                && x.StartTime < end
                && x.EndTime > start
                && (x.InstructorUserId == instructorUserId || x.VehicleId == vehicleId));

        if (excludeLessonId is > 0)
        {
            query = query.Where(x => x.Id != excludeLessonId);
        }

        if (await query.AnyAsync(ct))
        {
            throw new DomainException(
                "El instructor o el vehículo ya tienen una clase en ese horario.",
                400,
                "schedule_conflict");
        }
    }

    private async Task EnsureInstructorDailyHourLimitAsync(
        int schoolUserId,
        DateOnly sessionDate,
        TimeOnly start,
        TimeOnly end,
        int instructorUserId,
        int? excludeLessonId,
        CancellationToken ct)
    {
        var query = _db.Set<PracticalLessonSession>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.SessionDate == sessionDate
                && x.InstructorUserId == instructorUserId
                && x.Status != PracticalLessonStatuses.Cancelled);

        if (excludeLessonId is > 0)
        {
            query = query.Where(x => x.Id != excludeLessonId);
        }

        var sessions = await query.ToListAsync(ct);
        var used = sessions.Sum(s => GetLessonDurationHours(s.StartTime, s.EndTime));
        var adding = GetLessonDurationHours(start, end);

        if (used + adding > PracticalScheduleLimits.MaxDailyInstructorHours)
        {
            throw new DomainException(
                $"El instructor ya tiene {used:0.#} h programadas ese día. Máximo {PracticalScheduleLimits.MaxDailyInstructorHours} horas por día.",
                400,
                "daily_hour_limit");
        }
    }

    private static decimal GetLessonDurationHours(TimeOnly start, TimeOnly end)
    {
        var minutes = (end - start).TotalMinutes;
        if (minutes < 0)
        {
            return 0;
        }

        if (end.Minute == 59)
        {
            minutes += 1;
        }

        return (decimal)(minutes / 60.0);
    }

    private async Task<(int Completed, int Required, int NextNumber)> GetStudentLessonProgressAsync(
        int schoolUserId,
        int studentUserId,
        string? licenseCategories,
        CancellationToken ct)
    {
        var required = PracticalLessonRequirements.GetRequired(licenseCategories);
        var reservations = await _db.Set<PracticalLessonReservation>()
            .Include(x => x.LessonSession)
            .Where(x => x.StudentUserId == studentUserId
                && x.LessonSession!.SchoolUserId == schoolUserId
                && PracticalReservationStatuses.OccupiesSeatStatuses.Contains(x.Status))
            .OrderBy(x => x.LessonSession!.SessionDate)
            .ThenBy(x => x.LessonSession!.StartTime)
            .ToListAsync(ct);

        var completed = reservations.Count(x => x.Status == PracticalReservationStatuses.Attended);
        var nextNumber = Math.Min(required, reservations.Count + 1);
        return (completed, required, nextNumber);
    }

    private async Task<PracticalLessonAssignmentDto?> BuildAssignmentAsync(
        int schoolUserId,
        int lessonId,
        CancellationToken ct)
    {
        var reservation = await _db.Set<PracticalLessonReservation>()
            .Include(x => x.LessonSession)
            .FirstOrDefaultAsync(x => x.LessonSessionId == lessonId
                && PracticalReservationStatuses.OccupiesSeatStatuses.Contains(x.Status), ct);
        if (reservation is null)
        {
            return null;
        }

        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == reservation.StudentUserId, ct);
        var user = await _users.GetByIdAsync(reservation.StudentUserId, ct);
        var progress = await GetStudentLessonProgressAsync(
            schoolUserId,
            reservation.StudentUserId,
            enrollment?.LicenseCategories,
            ct);

        var ordered = await _db.Set<PracticalLessonReservation>()
            .Include(x => x.LessonSession)
            .Where(x => x.StudentUserId == reservation.StudentUserId
                && x.LessonSession!.SchoolUserId == schoolUserId
                && PracticalReservationStatuses.OccupiesSeatStatuses.Contains(x.Status))
            .OrderBy(x => x.LessonSession!.SessionDate)
            .ThenBy(x => x.LessonSession!.StartTime)
            .Select(x => x.Id)
            .ToListAsync(ct);

        var lessonNumber = ordered.IndexOf(reservation.Id) + 1;
        if (lessonNumber <= 0)
        {
            lessonNumber = progress.NextNumber;
        }

        return new PracticalLessonAssignmentDto(
            reservation.StudentUserId,
            user?.Name ?? $"Estudiante {reservation.StudentUserId}",
            PracticalLessonRequirements.PrimaryCategory(enrollment?.LicenseCategories),
            lessonNumber,
            progress.Required,
            reservation.Id,
            reservation.Status);
    }

    private async Task<PracticalLessonSessionDto> MapLessonAsync(
        PracticalLessonSession session,
        int? studentUserId,
        bool includeAssignment,
        CancellationToken ct)
    {
        var instructor = await _users.GetByIdAsync(session.InstructorUserId, ct);
        var reserved = await _db.Set<PracticalLessonReservation>()
            .CountAsync(x => x.LessonSessionId == session.Id
                && PracticalReservationStatuses.OccupiesSeatStatuses.Contains(x.Status), ct);
        var activeReserved = await _db.Set<PracticalLessonReservation>()
            .CountAsync(x => x.LessonSessionId == session.Id
                && PracticalReservationStatuses.ActiveStatuses.Contains(x.Status), ct);

        int? myReservationId = null;
        string? bookingState = null;
        string? bookingMessage = null;
        PracticalLessonAssignmentDto? assignment = null;

        if (includeAssignment)
        {
            assignment = await BuildAssignmentAsync(session.SchoolUserId, session.Id, ct);
        }

        if (studentUserId is int sid)
        {
            var mine = await _db.Set<PracticalLessonReservation>()
                .FirstOrDefaultAsync(x => x.LessonSessionId == session.Id
                    && x.StudentUserId == sid
                    && PracticalReservationStatuses.OccupiesSeatStatuses.Contains(x.Status), ct);
            if (mine is not null)
            {
                myReservationId = mine.Id;
                bookingState = mine.Status == PracticalReservationStatuses.Reserved
                    ? "reserved"
                    : "attended";
                bookingMessage = mine.Status switch
                {
                    PracticalReservationStatuses.Reserved => "Reservada",
                    PracticalReservationStatuses.Attended => "Asistió",
                    PracticalReservationStatuses.NoShow => "No asistió",
                    _ => mine.Status
                };
            }
            else if (activeReserved >= session.Capacity)
            {
                bookingState = "full";
                bookingMessage = "Sin cupos";
            }
            else
            {
                bookingState = "can_reserve";
                bookingMessage = $"{session.Capacity - activeReserved} cupo(s)";
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
            Math.Max(0, session.Capacity - activeReserved),
            session.Status,
            session.Notes,
            bookingState,
            bookingMessage,
            myReservationId,
            assignment);
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
