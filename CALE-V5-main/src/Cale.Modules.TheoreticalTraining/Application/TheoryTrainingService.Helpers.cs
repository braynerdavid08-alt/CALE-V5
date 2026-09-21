using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Domain;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.Catalog.Application.Abstractions;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed partial class TheoryTrainingService
{
    // ── Helpers ─────────────────────────────────────────────────────────

    private async Task<TheoryClassSessionDto> MapSessionAsync(
        int sessionId,
        int? studentUserId,
        CancellationToken ct,
        StudentScheduleContext? studentCtx = null)
    {
        var session = await _db.Set<TheoryClassSession>()
            .Include(x => x.Topic)
            .Include(x => x.Classroom)
            .FirstAsync(x => x.Id == sessionId, ct);

        var occupied = await CountOccupiedSeatsAsync(sessionId, ct);
        TheoryClassReservation? mine = null;
        if (studentUserId is > 0)
        {
            mine = await _db.Set<TheoryClassReservation>()
                .FirstOrDefaultAsync(x => x.ClassSessionId == sessionId
                    && x.StudentUserId == studentUserId
                    && TheoryReservationStatuses.OccupiesSeatStatuses.Contains(x.Status), ct);
        }

        string? instructorName = null;
        if (session.InstructorUserId is int instructorId)
        {
            instructorName = (await _users.GetByIdAsync(instructorId, ct))?.Name;
        }

        var (state, message) = ComputeBookingState(
            session,
            occupied,
            mine,
            _clock.UtcNow,
            studentCtx);
        return new TheoryClassSessionDto(
            session.Id,
            session.SessionDate,
            session.StartTime.ToString("HH:mm"),
            session.EndTime.ToString("HH:mm"),
            session.TopicId,
            session.Topic?.Name ?? "",
            session.Topic?.Color ?? "#3B82F6",
            session.ClassroomId,
            session.Classroom?.Name ?? "",
            session.Capacity,
            occupied,
            Math.Max(0, session.Capacity - occupied),
            session.Status,
            session.InstructorUserId,
            instructorName,
            session.Notes,
            session.ReservationOpenAt,
            session.ReservationCloseAt,
            state,
            message,
            mine?.Id,
            mine?.Status);
    }

    private static (string State, string Message) ComputeBookingState(
        TheoryClassSession session,
        int occupied,
        TheoryClassReservation? mine,
        DateTime nowUtc,
        StudentScheduleContext? studentCtx = null)
    {
        if (session.Status == TheoryClassStatuses.Cancelled)
        {
            return ("cancelled", "Cancelada");
        }

        var startUtc = ColombiaTime.ToUtc(session.SessionDate, session.StartTime);
        if (nowUtc >= startUtc)
        {
            return ("started", "Clase iniciada");
        }

        if (mine is not null && TheoryReservationStatuses.ActiveStatuses.Contains(mine.Status))
        {
            return ("reserved", "Reservada");
        }

        if (occupied >= session.Capacity)
        {
            return ("full", "Sin cupos");
        }

        if (nowUtc < session.ReservationOpenAt)
        {
            var colombiaNow = ColombiaTime.NowInColombia();
            var openLocal = TimeZoneInfo.ConvertTimeFromUtc(
                session.ReservationOpenAt,
                ColombiaTime.TimeZone);
            if (openLocal.Date == colombiaNow.Date.AddDays(1))
            {
                return ("locked_tomorrow", "Disponible mañana");
            }

            return ("locked", "Reservas aún no abiertas");
        }

        if (nowUtc > session.ReservationCloseAt)
        {
            return ("closed", "Reservas cerradas");
        }

        if (studentCtx is not null)
        {
            var access = EvaluateStudentAccess(studentCtx.Enrollment, session, studentCtx, session);
            if (access is not null)
            {
                return access.Value;
            }
        }

        var available = session.Capacity - occupied;
        if (available == 1)
        {
            return ("can_reserve", "Último cupo disponible");
        }

        return ("can_reserve", $"{available} cupos disponibles");
    }

    private async Task<int> CountOccupiedSeatsAsync(int sessionId, CancellationToken ct) =>
        await _db.Set<TheoryClassReservation>()
            .CountAsync(x => x.ClassSessionId == sessionId
                && TheoryReservationStatuses.OccupiesSeatStatuses.Contains(x.Status), ct);

    private async Task AcquireSessionLockAsync(int sessionId, CancellationToken ct)
    {
        if (_db.Database.IsNpgsql())
        {
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(@p0)",
                new object[] { sessionId },
                ct);
        }
    }

    private async Task ValidateNoScheduleConflictAsync(
        int studentUserId,
        TheoryClassSession target,
        SchoolStudentEnrollment enrollment,
        int? ignoreReservationId,
        TheoryBookingPolicy policy,
        CancellationToken ct)
    {
        var existing = await _db.Set<TheoryClassReservation>()
            .Include(x => x.ClassSession)
            .Where(x => x.StudentUserId == studentUserId
                && TheoryReservationStatuses.ActiveStatuses.Contains(x.Status)
                && (ignoreReservationId == null || x.Id != ignoreReservationId))
            .ToListAsync(ct);

        var saturdayGroup = enrollment.AttendanceDayType == StudentAttendanceDayTypes.Saturday;
        var targetIsSaturday = target.SessionDate.DayOfWeek == DayOfWeek.Saturday;
        var maxClasses = policy.MaxClassesFor(saturdayGroup, targetIsSaturday);

        foreach (var r in existing)
        {
            var s = r.ClassSession;
            if (s is null)
            {
                continue;
            }

            if (s.SessionDate != target.SessionDate)
            {
                continue;
            }

            if (ColombiaTime.TimesOverlap(
                    s.SessionDate, s.StartTime, s.EndTime,
                    target.SessionDate, target.StartTime, target.EndTime))
            {
                throw new DomainException(
                    "Ya tienes una clase que se cruza con ese horario.",
                    400,
                    "schedule_overlap");
            }
        }

        var countOnDay = existing.Count(x => x.ClassSession?.SessionDate == target.SessionDate);
        if (maxClasses > 0 && countOnDay >= maxClasses)
        {
            if (saturdayGroup && targetIsSaturday)
            {
                throw new DomainException(
                    $"Ya reservaste {maxClasses} clase(s) este sábado.",
                    400,
                    "saturday_day_limit");
            }

            throw new DomainException(
                maxClasses == 1
                    ? "Ya tienes una clase reservada ese día. Solo puedes reservar una por día."
                    : $"Ya reservaste {maxClasses} clases ese día.",
                400,
                "day_already_reserved");
        }

        if (policy.HasDailyMinutesLimit)
        {
            var usedMinutes = existing
                .Where(x => x.ClassSession?.SessionDate == target.SessionDate)
                .Sum(x => TheoryBookingPolicy.SessionDurationMinutes(
                    x.ClassSession!.StartTime,
                    x.ClassSession.EndTime));
            var targetMinutes = TheoryBookingPolicy.SessionDurationMinutes(target.StartTime, target.EndTime);
            if (usedMinutes + targetMinutes > policy.MaxDailyTheoryMinutes)
            {
                throw new DomainException(
                    $"Tu escuela permite máximo {policy.MaxDailyTheoryMinutes} minutos de teoría por día.",
                    400,
                    "daily_minutes_limit");
            }
        }
    }

    private static void ValidateReservationWindow(TheoryClassSession session)
    {
        if (session.Status != TheoryClassStatuses.Scheduled)
        {
            throw new DomainException("Esta clase no acepta reservas.", 400, "class_not_bookable");
        }

        var now = ColombiaTime.UtcNow;
        if (now < session.ReservationOpenAt)
        {
            throw new DomainException(
                "Las reservas para esta clase aún no están abiertas.",
                400,
                "reservation_not_open");
        }

        if (now > session.ReservationCloseAt)
        {
            throw new DomainException("Las reservas para esta clase ya cerraron.", 400, "reservation_closed");
        }
    }

    private async Task EnsureStudentCanReserveAsync(
        int studentUserId,
        int schoolUserId,
        CancellationToken ct)
    {
        var student = await _users.GetByIdAsync(studentUserId, ct)
            ?? throw new UnauthorizedException("Usuario no encontrado.", "unauthorized");
        if (!student.IsActive)
        {
            throw new ForbiddenException("Tu cuenta no está activa.", "account_inactive");
        }

        await EnsureSchoolMembershipActiveAsync(schoolUserId, ct);
        var enrollment = await GetEnrollmentAsync(schoolUserId, studentUserId, ct);
        if (!TrainingEligibilityService.CanStudentReserve(enrollment))
        {
            throw new ForbiddenException(
                "Tu escuela aún no te ha habilitado para reservar clases.",
                "enrollment_not_active");
        }
    }

    private static bool CanStudentReserve(SchoolStudentEnrollment enrollment) =>
        TrainingEligibilityService.CanStudentReserve(enrollment);

    private async Task<(int SchoolUserId, User Student)> ResolveStudentSchoolAsync(
        int studentUserId,
        CancellationToken ct)
    {
        var student = await _users.GetByIdAsync(studentUserId, ct)
            ?? throw new UnauthorizedException("Usuario no encontrado.", "unauthorized");
        if (student.SchoolId is not int schoolUserId)
        {
            throw new ForbiddenException(
                "Debes estar vinculado a una escuela.",
                "no_school");
        }

        return (schoolUserId, student);
    }

    private async Task<TheoryClassSession> RequireSessionAsync(
        int schoolUserId,
        int sessionId,
        CancellationToken ct) =>
        await _db.Set<TheoryClassSession>()
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.SchoolUserId == schoolUserId, ct)
            ?? throw new NotFoundException("Clase no encontrada.", "class_not_found");

    private async Task<TheoryTrainingSettings> GetOrCreateSettingsAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        try
        {
            return await GetOrCreateSettingsCoreAsync(schoolUserId, ct);
        }
        catch (Exception ex) when (IsLikelyMissingColumn(ex) || IsNullJsonColumnCast(ex))
        {
            if (!AllowRequestPathRepair)
            {
                throw;
            }

            _logger.LogWarning(
                ex,
                "Theory settings schema mismatch for school {SchoolUserId}; repairing columns",
                schoolUserId);
            _db.ChangeTracker.Clear();
            try
            {
                if (_db.Database.CurrentTransaction is not null)
                {
                    await _db.Database.RollbackTransactionAsync(ct);
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                await _db.Database.CloseConnectionAsync();
            }
            catch
            {
                // ignore
            }

            await FeatureSchema.EnsureTheoryTrainingColumnsAsync(_db, ct);
            await BackfillSettingsJsonNullsAsync(ct);
            _db.ChangeTracker.Clear();
            return await GetOrCreateSettingsCoreAsync(schoolUserId, ct);
        }
    }

    private async Task<TheoryTrainingSettings> GetOrCreateSettingsCoreAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        await BackfillSettingsJsonNullsAsync(ct);

        var settings = await _db.Set<TheoryTrainingSettings>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId, ct);
        if (settings is not null)
        {
            NormalizeSettingsJson(settings);
            return settings;
        }

        settings = new TheoryTrainingSettings
        {
            SchoolUserId = schoolUserId,
            RequiredTheoryHours = TheoryHourStandards.DefaultTheoryHours,
            RequiredWorkshopHours = TheoryHourStandards.DefaultWorkshopHours,
            LicenseCategoryPoliciesJson = "{}",
            SavedBookingPresetsJson = "[]",
            HiddenBookingPresetKeysJson = "[]",
            UpdatedAt = _clock.UtcNow
        };
        await _db.Set<TheoryTrainingSettings>().AddAsync(settings, ct);
        await _db.SaveChangesAsync(ct);
        return settings;
    }

    private async Task BackfillSettingsJsonNullsAsync(CancellationToken ct)
    {
        if (!_db.Database.IsNpgsql())
        {
            return;
        }

        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                """
                UPDATE "TheoryTrainingSettings"
                SET
                    "LicenseCategoryPoliciesJson" = COALESCE("LicenseCategoryPoliciesJson", '{}'),
                    "SavedBookingPresetsJson" = COALESCE("SavedBookingPresetsJson", '[]'),
                    "HiddenBookingPresetKeysJson" = COALESCE("HiddenBookingPresetKeysJson", '[]')
                WHERE "LicenseCategoryPoliciesJson" IS NULL
                   OR "SavedBookingPresetsJson" IS NULL
                   OR "HiddenBookingPresetKeysJson" IS NULL
                """,
                ct);
        }
        catch
        {
            // Columns may still be missing; EnsureTheoryTrainingColumnsAsync handles that.
        }
    }

    private static void NormalizeSettingsJson(TheoryTrainingSettings settings)
    {
        settings.LicenseCategoryPoliciesJson ??= "{}";
        settings.SavedBookingPresetsJson ??= "[]";
        settings.HiddenBookingPresetKeysJson ??= "[]";
    }

    private static bool IsLikelyMissingColumn(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            var msg = current.Message;
            if (msg.Contains("42703", StringComparison.Ordinal)
                || msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("Undefined column", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNullJsonColumnCast(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is InvalidCastException
                && current.Message.Contains("is null", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var msg = current.Message;
            if (msg.Contains("LicenseCategoryPoliciesJson", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("SavedBookingPresetsJson", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("HiddenBookingPresetKeysJson", StringComparison.OrdinalIgnoreCase))
            {
                if (msg.Contains("null", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private async Task EnsureSchoolMembershipActiveAsync(int schoolUserId, CancellationToken ct) =>
        await _membership.EnsureActiveAsync(schoolUserId, ct);

    private async Task<(decimal TheoryHours, decimal WorkshopHours, int Absences)> ComputeHoursBreakdownAsync(
        int schoolUserId,
        int studentUserId,
        CancellationToken ct)
    {
        var records = await _db.Set<TheoryAttendanceRecord>()
            .Include(x => x.ClassSession)!.ThenInclude(s => s!.Topic)
            .Where(x => x.StudentUserId == studentUserId
                && x.ClassSession != null
                && x.ClassSession.SchoolUserId == schoolUserId)
            .ToListAsync(ct);
        decimal theoryHours = 0;
        decimal workshopHours = 0;
        var absences = 0;
        foreach (var r in records)
        {
            if (r.Status is TheoryAttendanceStatuses.Present or TheoryAttendanceStatuses.Late)
            {
                var s = r.ClassSession;
                if (s is not null)
                {
                    var duration = (decimal)(s.EndTime - s.StartTime).TotalHours;
                    var category = s.Topic?.Category ?? TheoryTopicCategories.Theory;
                    if (category == TheoryTopicCategories.Workshop)
                    {
                        workshopHours += duration;
                    }
                    else
                    {
                        theoryHours += duration;
                    }
                }
            }
            else if (r.Status == TheoryAttendanceStatuses.Absent)
            {
                absences++;
            }
        }

        return (Math.Round(theoryHours, 1), Math.Round(workshopHours, 1), absences);
    }

    private async Task<PracticalEligibilityDto> GetPracticalEligibilityAsync(
        int schoolUserId,
        int studentUserId,
        TheoryTrainingSettings settings,
        decimal theoryHours,
        decimal workshopHours,
        bool theoryExamAuthorized,
        bool practicalAuthorized,
        string? licenseCategories,
        CancellationToken ct)
    {
        var theoryExamPassed = false;
        if (settings.TheoryExamId is int examId)
        {
            theoryExamPassed = await _db.Set<Attempt>()
                .AnyAsync(a => a.UserId == studentUserId
                    && a.ExamId == examId
                    && a.FinishedAt != null
                    && a.Passed, ct);
        }

        var (requiredTheoryHours, requiredWorkshopHours) = LicenseCategoryPolicyHelper.ResolveHourRequirements(
            settings,
            licenseCategories);
        var theoryComplete = theoryHours >= requiredTheoryHours;
        var workshopComplete = workshopHours >= requiredWorkshopHours;
        var canBook = theoryExamPassed && theoryComplete && workshopComplete && practicalAuthorized;

        string? blockReason = null;
        if (!theoryComplete)
        {
            blockReason = $"Te faltan horas de teoría ({theoryHours}/{requiredTheoryHours}).";
        }
        else if (!workshopComplete)
        {
            blockReason = $"Te faltan horas de taller ({workshopHours}/{requiredWorkshopHours}).";
        }
        else if (settings.TheoryExamId is null)
        {
            blockReason = "Tu escuela debe configurar el examen teórico oficial en Ajustes.";
        }
        else if (!theoryExamPassed)
        {
            blockReason = theoryExamAuthorized
                ? "Debes aprobar el examen teórico en la plataforma."
                : "Tu escuela debe autorizarte para presentar el examen teórico.";
        }
        else if (!practicalAuthorized)
        {
            blockReason = "Tu escuela debe autorizarte para clases de manejo.";
        }

        return new PracticalEligibilityDto(
            canBook,
            theoryExamPassed,
            theoryComplete,
            workshopComplete,
            theoryHours,
            requiredTheoryHours,
            workshopHours,
            requiredWorkshopHours,
            theoryExamAuthorized,
            practicalAuthorized,
            blockReason);
    }

    private async Task<(int Current, int Best)> ComputeStreaksAsync(
        int studentUserId,
        CancellationToken ct)
    {
        var dates = await _db.Set<StudentDailyCheckIn>()
            .Where(x => x.StudentUserId == studentUserId)
            .OrderByDescending(x => x.CheckInDate)
            .Select(x => x.CheckInDate)
            .ToListAsync(ct);
        if (dates.Count == 0)
        {
            return (0, 0);
        }

        var today = ColombiaTime.TodayInColombia();
        var current = 0;
        if (dates[0] == today || dates[0] == today.AddDays(-1))
        {
            var cursor = dates[0];
            foreach (var d in dates)
            {
                if (d == cursor)
                {
                    current++;
                    cursor = cursor.AddDays(-1);
                }
                else if (d < cursor)
                {
                    break;
                }
            }
        }

        var best = 0;
        var run = 0;
        DateOnly? prev = null;
        foreach (var d in dates.OrderBy(x => x))
        {
            if (prev is null || d == prev.Value.AddDays(1))
            {
                run++;
            }
            else
            {
                run = 1;
            }

            best = Math.Max(best, run);
            prev = d;
        }

        return (current, best);
    }

    private async Task<(string? Action, DateTime? OpensAt, string? Countdown)> ComputeNextActionAsync(
        int studentUserId,
        int schoolUserId,
        string? attendanceDayType,
        CancellationToken ct)
    {
        var tomorrow = ColombiaTime.TodayInColombia().AddDays(1);
        var sessions = await _db.Set<TheoryClassSession>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.SessionDate == tomorrow
                && x.Status == TheoryClassStatuses.Scheduled)
            .OrderBy(x => x.StartTime)
            .ToListAsync(ct);

        var session = sessions.FirstOrDefault(s =>
            attendanceDayType is null || SessionMatchesDayType(s.SessionDate, attendanceDayType));
        if (session is null)
        {
            return ("Consulta tu programación para planificar tu formación.", null, null);
        }

        var now = _clock.UtcNow;
        if (now < session.ReservationOpenAt)
        {
            var remaining = session.ReservationOpenAt - now;
            return (
                "Las reservas para mañana abrirán pronto.",
                session.ReservationOpenAt,
                $"{(int)remaining.TotalHours}h {remaining.Minutes}m");
        }

        var hasReservation = await _db.Set<TheoryClassReservation>()
            .AnyAsync(x => x.ClassSessionId == session.Id
                && x.StudentUserId == studentUserId
                && TheoryReservationStatuses.ActiveStatuses.Contains(x.Status), ct);
        if (!hasReservation)
        {
            return ("Ya puedes reservar tu clase de mañana.", null, null);
        }

        return ("Recuerda asistir a tu clase reservada.", null, null);
    }

    private static TheorySettingsDto MapSettings(TheoryTrainingSettings s)
    {
        NormalizeSettingsJson(s);
        return new(
            s.DefaultDurationMinutes,
            s.MinCancelHours,
            s.ReservationCloseMinutesBefore,
            TheoryHourStandards.DefaultTheoryHours,
            TheoryHourStandards.DefaultWorkshopHours,
            s.TheoryExamId,
            s.WeekdaysEnabled,
            s.SaturdayEnabled,
            s.MaxWeekdayClassesPerDay,
            s.MaxSaturdayClassesPerDay,
            s.MaxDailyTheoryMinutes,
            s.WeekdayReservationOpenDaysBefore,
            s.SaturdayReservationOpenDaysBefore,
            TheoryBookingPolicy.FormatOptionalTime(s.StudentBookingWindowStart),
            TheoryBookingPolicy.FormatOptionalTime(s.StudentBookingWindowEnd),
            TheoryBookingPolicy.Describe(s),
            LicenseCategoryPolicyHelper.BuildPolicyList(s),
            TheoryBookingPresetHelper.DeserializeSaved(s.SavedBookingPresetsJson),
            TheoryBookingPresetHelper.DeserializeHidden(s.HiddenBookingPresetKeysJson),
            s.NotifyReservationOpen,
            s.NotifyClassReminder24h,
            s.NotifyClassReminder1h,
            s.NotifyExamReminder24h);
    }

    private async Task LogAuthorizationEventAsync(
        int schoolUserId,
        int studentUserId,
        string authorizationType,
        string action,
        int? performedByUserId,
        CancellationToken ct)
    {
        await _db.Set<EnrollmentAuthorizationEvent>().AddAsync(new EnrollmentAuthorizationEvent
        {
            SchoolUserId = schoolUserId,
            StudentUserId = studentUserId,
            AuthorizationType = authorizationType,
            Action = action,
            PerformedByUserId = performedByUserId,
            CreatedAt = _clock.UtcNow
        }, ct);
    }

    private static EnrollmentDto MapEnrollmentDto(
        SchoolStudentEnrollment enrollment,
        string studentName,
        string studentEmail,
        PracticalEligibilityDto? eligibility = null,
        decimal balanceDue = 0) =>
        new(
            enrollment.Id,
            enrollment.StudentUserId,
            studentName,
            studentEmail,
            enrollment.Status,
            enrollment.AttendanceDayType,
            null,
            enrollment.LicenseCategories,
            enrollment.TheoryExamAuthorized,
            enrollment.PracticalAuthorized,
            enrollment.CreatedAt,
            enrollment.AcceptedAt,
            eligibility,
            balanceDue);

    private async Task<decimal> GetBalanceDueAsync(
        int schoolUserId,
        int studentUserId,
        CancellationToken ct)
    {
        var profile = await _db.Set<SchoolApprenticeProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId, ct);
        return profile?.BalanceDue ?? 0;
    }

    private Task NotifyTheoryExamAuthorizedAsync(
        int studentUserId,
        int enrollmentId,
        CancellationToken ct) =>
        _notifications.NotifyUsersAsync(
            [studentUserId],
            new NotificationDraft(
                "Autorizado para examen teórico",
                "Tu escuela te autorizó para presentar el examen teórico. Revisa Mi proceso para ver los siguientes pasos.",
                NotificationTypes.TheoryClass,
                RelatedEntity: "theory_exam_auth",
                RelatedId: enrollmentId,
                Link: "/student/training"),
            ct);

    private Task NotifyPracticalAuthorizedAsync(
        int studentUserId,
        int enrollmentId,
        CancellationToken ct) =>
        _notifications.NotifyUsersAsync(
            [studentUserId],
            new NotificationDraft(
                "Autorizado para clases de manejo",
                "Tu escuela te autorizó para programar y reservar clases de manejo. Entra a Práctica para agendar.",
                NotificationTypes.TheoryClass,
                RelatedEntity: "practical_auth",
                RelatedId: enrollmentId,
                Link: "/student/practical"),
            ct);

    private static TimeOnly ParseTime(string value)
    {
        if (TimeOnly.TryParse(value.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var time))
        {
            return time;
        }

        throw new DomainException("La hora no es válida.", 400, "invalid_time");
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }

    private sealed record StudentScheduleContext(
        SchoolStudentEnrollment? Enrollment,
        IReadOnlyDictionary<DateOnly, int> ReservationsPerDate,
        IReadOnlyDictionary<DateOnly, int> MinutesPerDate,
        TheoryBookingPolicy Policy);

    private async Task<StudentScheduleContext> BuildStudentScheduleContextAsync(
        int studentUserId,
        int schoolUserId,
        DateOnly weekStart,
        DateOnly weekEnd,
        CancellationToken ct)
    {
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId, ct);

        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        var policy = TheoryBookingPolicy.From(settings);

        var reservedSessions = await _db.Set<TheoryClassReservation>()
            .Include(x => x.ClassSession)
            .Where(x => x.StudentUserId == studentUserId
                && TheoryReservationStatuses.ActiveStatuses.Contains(x.Status)
                && x.ClassSession != null
                && x.ClassSession.SchoolUserId == schoolUserId
                && x.ClassSession.SessionDate >= weekStart
                && x.ClassSession.SessionDate <= weekEnd)
            .Select(x => new
            {
                x.ClassSession!.SessionDate,
                x.ClassSession.StartTime,
                x.ClassSession.EndTime
            })
            .ToListAsync(ct);

        var perDate = new Dictionary<DateOnly, int>();
        var minutesPerDate = new Dictionary<DateOnly, int>();
        foreach (var row in reservedSessions)
        {
            perDate[row.SessionDate] = perDate.GetValueOrDefault(row.SessionDate) + 1;
            var mins = TheoryBookingPolicy.SessionDurationMinutes(row.StartTime, row.EndTime);
            minutesPerDate[row.SessionDate] = minutesPerDate.GetValueOrDefault(row.SessionDate) + mins;
        }

        return new StudentScheduleContext(enrollment, perDate, minutesPerDate, policy);
    }

    private async Task<SchoolStudentEnrollment> GetEnrollmentAsync(
        int schoolUserId,
        int studentUserId,
        CancellationToken ct) =>
        await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId, ct)
        ?? throw new ForbiddenException(
            "Tu escuela aún no te ha habilitado para reservar clases.",
            "enrollment_not_active");

    private static void ValidateSessionDayForSettings(
        DateOnly sessionDate,
        TheoryTrainingSettings settings)
    {
        var isSaturday = sessionDate.DayOfWeek == DayOfWeek.Saturday;
        var isWeekday = sessionDate.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday;

        if (isSaturday)
        {
            if (!settings.SaturdayEnabled)
            {
                throw new DomainException("Las clases de sábado están desactivadas.", 400, "saturday_disabled");
            }

            return;
        }

        if (!isWeekday)
        {
            throw new DomainException(
                "La escuela solo programa clases de lunes a viernes o sábados.",
                400,
                "weekday_only");
        }

        if (!settings.WeekdaysEnabled)
        {
            throw new DomainException(
                "Las clases entre semana están desactivadas.",
                400,
                "weekday_disabled");
        }
    }

    private async Task<TheoryTrainingSettings> EnsureBothSchedulingGroupsAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        if (settings.WeekdaysEnabled && settings.SaturdayEnabled)
        {
            return settings;
        }

        settings.WeekdaysEnabled = true;
        settings.SaturdayEnabled = true;
        settings.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return settings;
    }

    private static void ActivateSchedulingGroup(TheoryTrainingSettings settings, string dayType)
    {
        if (dayType == StudentAttendanceDayTypes.Weekday)
        {
            settings.WeekdaysEnabled = true;
        }
        else if (dayType == StudentAttendanceDayTypes.Saturday)
        {
            settings.SaturdayEnabled = true;
        }
    }

    private static void ValidateStudentSessionAccess(
        SchoolStudentEnrollment enrollment,
        TheoryClassSession session)
    {
        var issue = EvaluateStudentAccessIssue(enrollment, session);
        if (issue is not null)
        {
            throw new DomainException(issue.Value.Message, 400, issue.Value.Code);
        }
    }

    private static (string State, string Message)? EvaluateStudentAccess(
        SchoolStudentEnrollment? enrollment,
        TheoryClassSession session,
        StudentScheduleContext ctx,
        TheoryClassSession? targetSession = null)
    {
        if (enrollment is null)
        {
            return ("locked", "No autorizado por la escuela");
        }

        if (enrollment.Status == StudentEnrollmentStatuses.Suspended)
        {
            return ("locked", "Tu acceso está suspendido. Pide a la escuela que te autorice de nuevo.");
        }

        if (!TrainingEligibilityService.CanStudentReserve(enrollment))
        {
            return ("locked", "Debes estar activo en Programación para reservar clases.");
        }

        if (ctx.Policy.HasBookingWindow)
        {
            var localTime = TimeOnly.FromDateTime(ColombiaTime.NowInColombia());
            if (!ctx.Policy.IsWithinBookingWindow(localTime))
            {
                return (
                    "locked",
                    $"Tu escuela permite reservar de {ctx.Policy.BookingWindowStart:HH\\:mm} a {ctx.Policy.BookingWindowEnd:HH\\:mm} (hora Colombia)");
            }
        }

        var dayLimit = EvaluateDayBookingLimit(enrollment, session, targetSession ?? session, ctx);
        if (dayLimit is not null)
        {
            return dayLimit.Value;
        }

        var issue = EvaluateStudentAccessIssue(enrollment, session);
        if (issue is not null)
        {
            return ("locked", issue.Value.Message);
        }

        return null;
    }

    private static (string State, string Message)? EvaluateDayBookingLimit(
        SchoolStudentEnrollment enrollment,
        TheoryClassSession session,
        TheoryClassSession targetSession,
        StudentScheduleContext ctx)
    {
        var policy = ctx.Policy;
        var count = ctx.ReservationsPerDate.GetValueOrDefault(session.SessionDate);
        var isSaturdaySession = session.SessionDate.DayOfWeek == DayOfWeek.Saturday;
        var isSaturdayStudent = enrollment.AttendanceDayType == StudentAttendanceDayTypes.Saturday;
        var maxClasses = policy.MaxClassesFor(isSaturdayStudent, isSaturdaySession);

        if (maxClasses > 0 && count >= maxClasses)
        {
            if (isSaturdaySession && isSaturdayStudent)
            {
                return (
                    "day_limit",
                    $"Ya reservaste {maxClasses} clase(s) este sábado");
            }

            if (maxClasses == 1)
            {
                return ("day_taken", "Ya reservaste una clase este día");
            }

            return ("day_limit", $"Ya reservaste {maxClasses} clases este día");
        }

        if (policy.HasDailyMinutesLimit)
        {
            var usedMinutes = ctx.MinutesPerDate.GetValueOrDefault(session.SessionDate);
            var addMinutes = TheoryBookingPolicy.SessionDurationMinutes(
                targetSession.StartTime,
                targetSession.EndTime);
            if (usedMinutes + addMinutes > policy.MaxDailyTheoryMinutes)
            {
                return (
                    "minutes_limit",
                    $"Tu escuela permite máximo {policy.MaxDailyTheoryMinutes} min de teoría por día");
            }
        }

        return null;
    }

    private static (string Code, string Message)? EvaluateStudentAccessIssue(
        SchoolStudentEnrollment enrollment,
        TheoryClassSession session)
    {
        if (enrollment.AttendanceDayType is null)
        {
            return ("day_not_assigned", "Día de asistencia no asignado");
        }

        var sessionDayType = session.SessionDate.DayOfWeek == DayOfWeek.Saturday
            ? StudentAttendanceDayTypes.Saturday
            : StudentAttendanceDayTypes.Weekday;

        if (!string.Equals(enrollment.AttendanceDayType, sessionDayType, StringComparison.OrdinalIgnoreCase))
        {
            var groupLabel = StudentAttendanceDayTypes.FormatLabel(enrollment.AttendanceDayType);
            return enrollment.AttendanceDayType == StudentAttendanceDayTypes.Weekday
                ? ("day_not_allowed", "Tu grupo es Semana; no puedes reservar los sábados")
                : ("day_not_allowed", $"Tu grupo es {groupLabel}; no puedes reservar entre semana");
        }

        return null;
    }

    private static bool SessionMatchesDayType(DateOnly sessionDate, string dayType)
    {
        var sessionDayType = sessionDate.DayOfWeek == DayOfWeek.Saturday
            ? StudentAttendanceDayTypes.Saturday
            : StudentAttendanceDayTypes.Weekday;

        return string.Equals(sessionDayType, dayType, StringComparison.OrdinalIgnoreCase);
    }
}
