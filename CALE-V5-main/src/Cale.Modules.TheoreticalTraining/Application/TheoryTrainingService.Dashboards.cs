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
    // ── Dashboards ──────────────────────────────────────────────────────

    public async Task<TheorySchoolDashboardDto> GetSchoolDashboardAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var today = ColombiaTime.TodayInColombia();
        var sessionsToday = await _db.Set<TheoryClassSession>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.SessionDate == today
                && x.Status != TheoryClassStatuses.Cancelled)
            .ToListAsync(ct);
        var sessionIds = sessionsToday.Select(x => x.Id).ToList();
        var reserved = sessionIds.Count == 0
            ? 0
            : await _db.Set<TheoryClassReservation>()
                .CountAsync(x => sessionIds.Contains(x.ClassSessionId)
                    && TheoryReservationStatuses.OccupiesSeatStatuses.Contains(x.Status), ct);
        var capacity = sessionsToday.Sum(x => x.Capacity);
        var absences = sessionIds.Count == 0
            ? 0
            : await _db.Set<TheoryAttendanceRecord>()
                .CountAsync(x => sessionIds.Contains(x.ClassSessionId)
                    && x.Status == TheoryAttendanceStatuses.Absent, ct);
        var scheduled = await _db.Set<TheoryClassSession>()
            .CountAsync(x => x.SchoolUserId == schoolUserId
                && x.Status == TheoryClassStatuses.Scheduled
                && x.SessionDate >= today, ct);

        return new TheorySchoolDashboardDto(
            sessionsToday.Count,
            reserved,
            Math.Max(0, capacity - reserved),
            absences,
            scheduled);
    }

    public async Task<TheoryStudentDashboardDto> GetStudentDashboardAsync(
        int studentUserId,
        CancellationToken ct)
    {
        var (schoolUserId, _) = await ResolveStudentSchoolAsync(studentUserId, ct);
        try
        {
            return await BuildStudentDashboardAsync(schoolUserId, studentUserId, ct);
        }
        catch
        {
            // Theory tables may be mid-migration on older Postgres DBs.
            return new TheoryStudentDashboardDto(
                null,
                Array.Empty<TheoryClassSessionDto>(),
                0,
                0,
                20,
                0,
                10,
                0,
                0,
                0,
                0,
                "Estamos preparando tu formación. Recarga en unos minutos.",
                null,
                null,
                false,
                Array.Empty<TheoryDailyTaskDto>(),
                null,
                null,
                null,
                null);
        }
    }

    private async Task<TheoryStudentDashboardDto> BuildStudentDashboardAsync(
        int schoolUserId,
        int studentUserId,
        CancellationToken ct)
    {
        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        var today = ColombiaTime.TodayInColombia();
        var nowUtc = _clock.UtcNow;

        var myReservations = await _db.Set<TheoryClassReservation>()
            .Include(x => x.ClassSession)!.ThenInclude(s => s!.Topic)
            .Include(x => x.ClassSession)!.ThenInclude(s => s!.Classroom)
            .Where(x => x.StudentUserId == studentUserId
                && TheoryReservationStatuses.OccupiesSeatStatuses.Contains(x.Status))
            .ToListAsync(ct);

        var upcoming = myReservations
            .Where(r => r.ClassSession is not null)
            .Select(r => r.ClassSession!)
            .Where(s => ColombiaTime.ToUtc(s.SessionDate, s.StartTime) >= nowUtc
                && s.Status != TheoryClassStatuses.Cancelled)
            .OrderBy(s => s.SessionDate)
            .ThenBy(s => s.StartTime)
            .ToList();

        TheoryClassSessionDto? nextDto = null;
        if (upcoming.Count > 0)
        {
            nextDto = await MapSessionAsync(upcoming[0].Id, studentUserId, ct);
        }

        var upcomingDtos = new List<TheoryClassSessionDto>();
        foreach (var s in upcoming.Take(8))
        {
            upcomingDtos.Add(await MapSessionAsync(s.Id, studentUserId, ct));
        }

        var (theoryHours, workshopHours, absences) = await ComputeHoursBreakdownAsync(
            schoolUserId,
            studentUserId,
            ct);
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId, ct);
        var (hoursRequired, workshopRequired) = LicenseCategoryPolicyHelper.ResolveHourRequirements(
            settings,
            enrollment?.LicenseCategories);
        var progress = hoursRequired <= 0
            ? 0
            : Math.Round(theoryHours / hoursRequired * 100m, 1);

        var eligibility = await GetPracticalEligibilityAsync(
            schoolUserId,
            studentUserId,
            settings,
            theoryHours,
            workshopHours,
            enrollment?.TheoryExamAuthorized ?? false,
            enrollment?.PracticalAuthorized ?? false,
            enrollment?.LicenseCategories,
            ct);

        var (currentStreak, bestStreak) = await ComputeStreaksAsync(studentUserId, ct);
        var checkedIn = await _db.Set<StudentDailyCheckIn>()
            .AnyAsync(x => x.StudentUserId == studentUserId && x.CheckInDate == today, ct);

        var (nextAction, opensAt, countdownLabel) = await ComputeNextActionAsync(
            studentUserId,
            schoolUserId,
            enrollment?.AttendanceDayType,
            ct);

        var tasks = new List<TheoryDailyTaskDto>
        {
            new("Check-in diario", checkedIn),
            new("Revisar próxima clase", nextDto is not null),
            new("Reservar clase de mañana", false)
        };

        StudentExamAppointmentDto? nextExamAppointment = null;
        var examSlot = await _db.Set<TheoryExamAppointment>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId
                && x.ExamDate >= today)
            .OrderBy(x => x.ExamDate)
            .ThenBy(x => x.SlotTime)
            .FirstOrDefaultAsync(ct);
        if (examSlot is not null)
        {
            nextExamAppointment = new StudentExamAppointmentDto(
                examSlot.Id,
                examSlot.ExamDate.ToString("yyyy-MM-dd"),
                examSlot.SlotTime.ToString("HH:mm"));
        }

        StudentPlatformExamDto? platformExam = null;
        if (settings.TheoryExamId is int platformExamId)
        {
            var exams = await _catalog.ListPublishedExamsAsync(ct);
            var match = exams.FirstOrDefault(e => e.Id == platformExamId);
            if (match is not null)
            {
                platformExam = new StudentPlatformExamDto(match.Id, match.Name);
            }
        }

        var profile = await _db.Set<SchoolApprenticeProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId,
                ct);

        return new TheoryStudentDashboardDto(
            nextDto,
            upcomingDtos,
            progress,
            theoryHours,
            hoursRequired,
            workshopHours,
            workshopRequired,
            Math.Max(0, upcoming.Count),
            absences,
            currentStreak,
            bestStreak,
            nextAction,
            countdownLabel,
            opensAt,
            checkedIn,
            tasks,
            enrollment?.AttendanceDayType,
            eligibility,
            nextExamAppointment,
            platformExam,
            profile?.BalanceDue ?? 0);
    }

    public async Task<TheoryWeekScheduleDto> GetStudentWeekScheduleAsync(
        int studentUserId,
        DateOnly? weekStart,
        CancellationToken ct)
    {
        var (schoolUserId, _) = await ResolveStudentSchoolAsync(studentUserId, ct);
        return await GetWeekScheduleAsync(schoolUserId, weekStart, studentUserId, ct);
    }

    public async Task CheckInAsync(int studentUserId, CancellationToken ct)
    {
        var today = ColombiaTime.TodayInColombia();
        var exists = await _db.Set<StudentDailyCheckIn>()
            .AnyAsync(x => x.StudentUserId == studentUserId && x.CheckInDate == today, ct);
        if (exists)
        {
            return;
        }

        await _db.Set<StudentDailyCheckIn>().AddAsync(new StudentDailyCheckIn
        {
            StudentUserId = studentUserId,
            CheckInDate = today,
            CheckInAt = _clock.UtcNow
        }, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TheoryExamOptionDto>> ListExamOptionsAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var members = await _users.ListBySchoolAsync(schoolUserId, ct);
        var ownerIds = members
            .Where(m => m.Role is Roles.Teacher or Roles.School)
            .Select(m => m.Id)
            .ToHashSet();

        var exams = await _catalog.ListPublishedExamsAsync(ct);
        return exams
            .Where(e => ownerIds.Contains(e.CreatedById))
            .OrderBy(e => e.Name)
            .Select(e => new TheoryExamOptionDto(e.Id, e.Name))
            .ToList();
    }

    public async Task<(int SchoolUserId, User Student)> ResolveStudentSchoolPublicAsync(
        int studentUserId,
        CancellationToken ct) =>
        await ResolveStudentSchoolAsync(studentUserId, ct);

    public async Task<PracticalEligibilityDto> GetPracticalEligibilityAsync(
        int schoolUserId,
        int studentUserId,
        CancellationToken ct)
    {
        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        var (theoryHours, workshopHours, _) = await ComputeHoursBreakdownAsync(
            schoolUserId,
            studentUserId,
            ct);
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId, ct);
        return await GetPracticalEligibilityAsync(
            schoolUserId,
            studentUserId,
            settings,
            theoryHours,
            workshopHours,
            enrollment?.TheoryExamAuthorized ?? false,
            enrollment?.PracticalAuthorized ?? false,
            enrollment?.LicenseCategories,
            ct);
    }

    public async Task<(int ReadyForExamCount, int ReadyForPracticalCount, int NoExamAppointmentCount,
        IReadOnlyList<SchoolDashboardStudentRowDto> TopReadyForExam,
        IReadOnlyList<SchoolDashboardStudentRowDto> TopNoExamAppointment)> GetEnrollmentPipelineStatsAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        try
        {
            return await GetEnrollmentPipelineStatsCoreAsync(schoolUserId, ct);
        }
        catch (Exception ex) when (IsLikelyMissingColumn(ex))
        {
            if (!AllowRequestPathRepair)
            {
                throw;
            }

            _logger.LogWarning(ex, "Pipeline stats schema mismatch; repairing");
            await FeatureSchema.EnsureTheoryTrainingColumnsAsync(_db, ct);
            _db.ChangeTracker.Clear();
            return await GetEnrollmentPipelineStatsCoreAsync(schoolUserId, ct);
        }
    }

    private async Task<(int ReadyForExamCount, int ReadyForPracticalCount, int NoExamAppointmentCount,
        IReadOnlyList<SchoolDashboardStudentRowDto> TopReadyForExam,
        IReadOnlyList<SchoolDashboardStudentRowDto> TopNoExamAppointment)> GetEnrollmentPipelineStatsCoreAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        var students = (await _users.ListBySchoolAsync(schoolUserId, ct))
            .Where(x => x.Role == Roles.Student)
            .ToDictionary(x => x.Id);

        var enrollments = await _db.Set<SchoolStudentEnrollment>()
            .Where(x => x.SchoolUserId == schoolUserId
                && StudentEnrollmentStatuses.CanReserveStatuses.Contains(x.Status))
            .ToListAsync(ct);

        var balances = await _db.Set<SchoolApprenticeProfile>()
            .Where(x => x.SchoolUserId == schoolUserId)
            .ToListAsync(ct);
        var balanceByStudent = balances
            .GroupBy(x => x.StudentUserId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First().BalanceDue);

        var today = DateOnly.FromDateTime(_clock.UtcNow.Date);
        var studentsWithAppointment = await _db.Set<TheoryExamAppointment>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.ExamDate >= today
                && x.StudentUserId != null)
            .Select(x => x.StudentUserId!.Value)
            .Distinct()
            .ToListAsync(ct);
        var appointmentSet = studentsWithAppointment.ToHashSet();

        var readyForExam = new List<SchoolDashboardStudentRowDto>();
        var readyForPractical = 0;
        var noExamAppointment = new List<SchoolDashboardStudentRowDto>();

        foreach (var enrollment in enrollments)
        {
            if (!students.TryGetValue(enrollment.StudentUserId, out var student))
            {
                continue;
            }

            balanceByStudent.TryGetValue(enrollment.StudentUserId, out var balanceDue);
            var (theoryHours, workshopHours, _) = await ComputeHoursBreakdownAsync(
                schoolUserId,
                enrollment.StudentUserId,
                ct);
            var eligibility = await GetPracticalEligibilityAsync(
                schoolUserId,
                enrollment.StudentUserId,
                settings,
                theoryHours,
                workshopHours,
                enrollment.TheoryExamAuthorized,
                enrollment.PracticalAuthorized,
                enrollment.LicenseCategories,
                ct);

            if (balanceDue <= 0
                && settings.TheoryExamId is not null
                && !enrollment.TheoryExamAuthorized
                && eligibility.TheoryHoursComplete
                && eligibility.WorkshopHoursComplete
                && !eligibility.TheoryExamPassed)
            {
                readyForExam.Add(new SchoolDashboardStudentRowDto(
                    enrollment.StudentUserId,
                    student.Name));
            }

            if (balanceDue <= 0
                && !enrollment.PracticalAuthorized
                && eligibility.TheoryExamPassed)
            {
                readyForPractical++;
            }

            if (enrollment.TheoryExamAuthorized
                && !eligibility.TheoryExamPassed
                && !appointmentSet.Contains(enrollment.StudentUserId))
            {
                noExamAppointment.Add(new SchoolDashboardStudentRowDto(
                    enrollment.StudentUserId,
                    student.Name));
            }
        }

        return (
            readyForExam.Count,
            readyForPractical,
            noExamAppointment.Count,
            readyForExam.Take(5).ToList(),
            noExamAppointment.Take(5).ToList());
    }

    public async Task OnPlatformTheoryExamPassedAsync(
        int studentUserId,
        int examId,
        CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(studentUserId, ct);
        if (user?.SchoolId is not int schoolUserId)
        {
            return;
        }

        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        if (settings.TheoryExamId != examId)
        {
            return;
        }

        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId, ct);
        if (enrollment is null || !enrollment.TheoryExamAuthorized)
        {
            return;
        }

        var now = _clock.UtcNow;
        enrollment.TheoryExamAuthorized = false;
        enrollment.TheoryExamAuthorizedAt = null;
        enrollment.UpdatedAt = now;
        await LogAuthorizationEventAsync(
            schoolUserId,
            studentUserId,
            EnrollmentAuthorizationTypes.TheoryExam,
            EnrollmentAuthorizationActions.Revoked,
            null,
            ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<EnrollmentAuthorizationEventDto>> ListAuthorizationHistoryAsync(
        int schoolUserId,
        int? studentUserId,
        int limit,
        CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 100);
        var query = _db.Set<EnrollmentAuthorizationEvent>()
            .Where(x => x.SchoolUserId == schoolUserId);
        if (studentUserId is int sid)
        {
            query = query.Where(x => x.StudentUserId == sid);
        }

        var events = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
        var performerIds = events
            .Where(x => x.PerformedByUserId is > 0)
            .Select(x => x.PerformedByUserId!.Value)
            .Distinct()
            .ToList();
        var performerNames = new Dictionary<int, string>();
        foreach (var id in performerIds)
        {
            performerNames[id] = (await _users.GetByIdAsync(id, ct))?.Name ?? "Usuario";
        }

        return events
            .Select(e => new EnrollmentAuthorizationEventDto(
                e.AuthorizationType,
                e.Action,
                e.PerformedByUserId is int pid
                    ? performerNames.GetValueOrDefault(pid, "Usuario")
                    : "Sistema",
                e.CreatedAt))
            .ToList();
    }
}
