using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Auth;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.Identity.Domain;
using Cale.Modules.TheoreticalTraining.Application.DTOs;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed class ApprenticeRegistryService
{
    private readonly CaleDbContext _db;
    private readonly IUserStore _users;
    private readonly IClock _clock;
    private readonly TheoryTrainingService _theory;
    private readonly PracticalTrainingService _practical;
    private readonly INotificationPublisher _notifications;
    private readonly ITrainingEligibilityService _eligibility;
    private readonly ISchoolMembershipGuard _membership;

    public ApprenticeRegistryService(
        CaleDbContext db,
        IUserStore users,
        IClock clock,
        TheoryTrainingService theory,
        PracticalTrainingService practical,
        INotificationPublisher notifications,
        ITrainingEligibilityService eligibility,
        ISchoolMembershipGuard membership)
    {
        _db = db;
        _users = users;
        _clock = clock;
        _theory = theory;
        _practical = practical;
        _notifications = notifications;
        _eligibility = eligibility;
        _membership = membership;
    }

    public async Task<IReadOnlyList<ApprenticeDto>> ListAsync(
        int schoolUserId,
        string? search,
        string? month,
        bool? withBalance,
        CancellationToken ct)
    {
        var students = await _users.ListBySchoolAsync(schoolUserId, ct);
        var studentMap = students
            .Where(x => x.Role == Roles.Student)
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First());
        var enrollments = await _db.Set<SchoolStudentEnrollment>()
            .Where(x => x.SchoolUserId == schoolUserId)
            .ToListAsync(ct);
        var enrollmentMap = enrollments
            .GroupBy(x => x.StudentUserId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());
        var profiles = await _db.Set<SchoolApprenticeProfile>()
            .Where(x => x.SchoolUserId == schoolUserId)
            .ToListAsync(ct);
        var profileMap = profiles
            .GroupBy(x => x.StudentUserId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

        var ids = studentMap.Keys.Union(profileMap.Keys).Distinct();
        var rows = new List<ApprenticeDto>();
        foreach (var studentUserId in ids)
        {
            if (!studentMap.TryGetValue(studentUserId, out var user))
            {
                continue;
            }

            profileMap.TryGetValue(studentUserId, out var profile);
            enrollmentMap.TryGetValue(studentUserId, out var enrollment);
            rows.Add(MapDto(studentUserId, user.Name, user.Email ?? "", profile, enrollment));
        }

        IEnumerable<ApprenticeDto> query = rows.OrderBy(x => x.StudentName);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.StudentName.ToLowerInvariant().Contains(q)
                || (x.DocumentNumber ?? "").Contains(q)
                || (x.Phone ?? "").Contains(q));
        }

        if (!string.IsNullOrWhiteSpace(month))
        {
            query = query.Where(x =>
                string.Equals(x.EnrollmentMonth, month, StringComparison.OrdinalIgnoreCase));
        }

        if (withBalance == true)
        {
            query = query.Where(x => x.BalanceDue > 0);
        }

        return query.ToList();
    }

    public async Task<ApprenticeDetailDto> GetDetailAsync(
        int schoolUserId,
        int studentUserId,
        CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(studentUserId, ct)
            ?? throw new NotFoundException("Estudiante no encontrado.", "student_not_found");
        if (!await BelongsToSchoolAsync(schoolUserId, studentUserId, user, ct))
        {
            throw new DomainException("El estudiante no pertenece a tu escuela.", 400, "invalid_student");
        }

        var profile = await _db.Set<SchoolApprenticeProfile>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId, ct);
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId, ct);
        var apprentice = MapDto(studentUserId, user.Name, user.Email ?? "", profile, enrollment);
        PracticalEligibilityDto training;
        try
        {
            training = await _theory.GetPracticalEligibilityAsync(schoolUserId, studentUserId, ct);
        }
        catch (Exception)
        {
            await ResetDbConnectionAsync(ct);
            training = new PracticalEligibilityDto(
                false,
                false,
                false,
                false,
                0,
                0,
                0,
                0,
                enrollment?.TheoryExamAuthorized ?? false,
                enrollment?.PracticalAuthorized ?? false,
                "No se pudo calcular el progreso. Revisa Programación teórica.");
        }

        ApprenticePracticalSummaryDto practical;
        try
        {
            practical = await _practical.GetApprenticePracticalSummaryAsync(schoolUserId, studentUserId, ct);
        }
        catch (Exception)
        {
            await ResetDbConnectionAsync(ct);
            practical = new ApprenticePracticalSummaryDto(0, 0, 0, null, null);
        }

        ApprenticeExamSummaryDto? nextExam = null;
        try
        {
            var today = DateOnly.FromDateTime(_clock.UtcNow.Date);
            var exam = await _db.Set<TheoryExamAppointment>()
                .Where(x => x.SchoolUserId == schoolUserId
                    && x.StudentUserId == studentUserId
                    && x.ExamDate >= today)
                .OrderBy(x => x.ExamDate)
                .ThenBy(x => x.SlotTime)
                .FirstOrDefaultAsync(ct);

            nextExam = exam is null
                ? null
                : new ApprenticeExamSummaryDto(
                    exam.Id,
                    exam.ExamDate.ToString("yyyy-MM-dd"),
                    exam.SlotTime.ToString("HH:mm"));
        }
        catch (Exception)
        {
            await ResetDbConnectionAsync(ct);
            nextExam = null;
        }

        IReadOnlyList<EnrollmentAuthorizationEventDto> authHistory;
        try
        {
            authHistory = await _theory.ListAuthorizationHistoryAsync(
                schoolUserId,
                studentUserId,
                15,
                ct);
        }
        catch (Exception)
        {
            await ResetDbConnectionAsync(ct);
            authHistory = [];
        }

        return new ApprenticeDetailDto(apprentice, training, practical, nextExam, authHistory);
    }

    private async Task ResetDbConnectionAsync(CancellationToken ct)
    {
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
            // No ambient transaction.
        }

        try
        {
            await _db.Database.CloseConnectionAsync();
        }
        catch
        {
            // Already closed.
        }
    }

    public async Task<SchoolOperationsDashboardDto> GetDashboardAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        try
        {
            return await GetDashboardCoreAsync(schoolUserId, ct);
        }
        catch (Exception)
        {
            // Schema drift mid-deploy: repair theory columns and retry once.
            await FeatureSchema.EnsureTheoryTrainingColumnsAsync(_db, ct);
            _db.ChangeTracker.Clear();
            try
            {
                return await GetDashboardCoreAsync(schoolUserId, ct);
            }
            catch
            {
                return new SchoolOperationsDashboardDto(
                    0, 0, 0, 0, 0, 0, 0, 0,
                    Array.Empty<SchoolDashboardBalanceRowDto>(),
                    Array.Empty<SchoolDashboardStudentRowDto>(),
                    Array.Empty<SchoolDashboardStudentRowDto>(),
                    Array.Empty<TheoryExamSlotDto>());
            }
        }
    }

    private async Task<SchoolOperationsDashboardDto> GetDashboardCoreAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var students = await _users.ListBySchoolAsync(schoolUserId, ct);
        var studentMap = students
            .Where(x => x.Role == Roles.Student)
            .ToDictionary(x => x.Id);

        var profiles = await _db.Set<SchoolApprenticeProfile>()
            .Where(x => x.SchoolUserId == schoolUserId)
            .ToListAsync(ct);

        var apprenticeCount = studentMap.Count;
        var balanceRows = new List<SchoolDashboardBalanceRowDto>();

        foreach (var profile in profiles.Where(x => x.BalanceDue > 0))
        {
            if (!studentMap.TryGetValue(profile.StudentUserId, out var user))
            {
                continue;
            }

            balanceRows.Add(new SchoolDashboardBalanceRowDto(
                profile.StudentUserId,
                user.Name,
                profile.BalanceDue));
        }

        var topBalanceDue = balanceRows
            .Where(x => x.BalanceDue > 0)
            .OrderByDescending(x => x.BalanceDue)
            .Take(5)
            .ToList();

        var balancePendingCount = balanceRows.Count(x => x.BalanceDue > 0);
        var balancePendingTotal = balanceRows.Sum(x => x.BalanceDue);
        var pendingEnrollmentCount = profiles.Count(x => !x.IsEnrolled);

        var today = DateOnly.FromDateTime(_clock.UtcNow.Date);
        var weekEnd = today.AddDays(7);
        var examSlots = await _db.Set<TheoryExamAppointment>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.ExamDate >= today
                && x.ExamDate <= weekEnd)
            .OrderBy(x => x.ExamDate)
            .ThenBy(x => x.SlotTime)
            .ToListAsync(ct);

        var upcomingExams = new List<TheoryExamSlotDto>();
        foreach (var slot in examSlots.Take(6))
        {
            string? studentName = null;
            if (slot.StudentUserId is int sid)
            {
                studentName = studentMap.TryGetValue(sid, out var user) ? user.Name : null;
                if (studentName is null)
                {
                    studentName = (await _users.GetByIdAsync(sid, ct))?.Name;
                }
            }

            upcomingExams.Add(new TheoryExamSlotDto(
                slot.Id,
                slot.ExamDate.ToString("yyyy-MM-dd"),
                slot.SlotTime.ToString("HH:mm"),
                slot.StudentUserId,
                slot.StudentLabel,
                studentName,
                slot.Notes));
        }

        var pipeline = await _theory.GetEnrollmentPipelineStatsAsync(schoolUserId, ct);

        return new SchoolOperationsDashboardDto(
            apprenticeCount,
            balancePendingCount,
            balancePendingTotal,
            examSlots.Count,
            pendingEnrollmentCount,
            pipeline.ReadyForExamCount,
            pipeline.ReadyForPracticalCount,
            pipeline.NoExamAppointmentCount,
            topBalanceDue,
            pipeline.TopReadyForExam,
            pipeline.TopNoExamAppointment,
            upcomingExams);
    }

    public async Task<ApprenticeDto> UpdateAsync(
        int schoolUserId,
        int studentUserId,
        SaveApprenticeRequest request,
        CancellationToken ct)
    {
        await _membership.EnsureActiveAsync(schoolUserId, ct);
        var user = await _users.GetByIdAsync(studentUserId, ct)
            ?? throw new NotFoundException("Estudiante no encontrado.", "student_not_found");
        if (!await BelongsToSchoolAsync(schoolUserId, studentUserId, user, ct))
        {
            throw new DomainException("El estudiante no pertenece a tu escuela.", 400, "invalid_student");
        }

        var now = _clock.UtcNow;
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId, ct);
        if (enrollment is null)
        {
            enrollment = new SchoolStudentEnrollment
            {
                SchoolUserId = schoolUserId,
                StudentUserId = studentUserId,
                Status = StudentEnrollmentStatuses.Pending,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _db.Set<SchoolStudentEnrollment>().AddAsync(enrollment, ct);
        }

        if (!string.IsNullOrWhiteSpace(request.LicenseCategories))
        {
            var categories = request.LicenseCategories.Trim();
            if (!StudentLicenseCategories.IsValid(categories))
            {
                throw new DomainException("Categoría de licencia no válida.", 400, "invalid_license_category");
            }

            enrollment.LicenseCategories = StudentLicenseCategories.Presets
                .First(p => p.Equals(categories, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.AttendanceDayType))
        {
            var dayType = request.AttendanceDayType.Trim();
            if (!StudentAttendanceDayTypes.IsValid(dayType))
            {
                throw new DomainException("Tipo de día no válido.", 400, "invalid_day_type");
            }

            enrollment.AttendanceDayType = dayType;
            // Do not EF-load TheoryTrainingSettings (JSON columns may be mid-repair and
            // abort the whole SaveChanges, including payments). Toggle flags via SQL.
            try
            {
                if (dayType == StudentAttendanceDayTypes.Weekday)
                {
                    await _db.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE "TheoryTrainingSettings"
                        SET "WeekdaysEnabled" = TRUE, "UpdatedAt" = {now}
                        WHERE "SchoolUserId" = {schoolUserId}
                        """,
                        ct);
                }
                else if (dayType == StudentAttendanceDayTypes.Saturday)
                {
                    await _db.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE "TheoryTrainingSettings"
                        SET "SaturdayEnabled" = TRUE, "UpdatedAt" = {now}
                        WHERE "SchoolUserId" = {schoolUserId}
                        """,
                        ct);
                }
            }
            catch
            {
                // Settings row/columns may be absent; profile/payment still persist.
            }
        }

        if (enrollment.Status is StudentEnrollmentStatuses.Pending or StudentEnrollmentStatuses.Accepted
            && !string.IsNullOrWhiteSpace(enrollment.AttendanceDayType)
            && !string.IsNullOrWhiteSpace(enrollment.LicenseCategories))
        {
            enrollment.Status = StudentEnrollmentStatuses.Active;
            enrollment.AcceptedAt ??= now;
        }

        enrollment.UpdatedAt = now;

        var profile = await _db.Set<SchoolApprenticeProfile>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId, ct);
        if (profile is null)
        {
            profile = new SchoolApprenticeProfile
            {
                SchoolUserId = schoolUserId,
                StudentUserId = studentUserId,
                CreatedAt = now
            };
            await _db.Set<SchoolApprenticeProfile>().AddAsync(profile, ct);
        }

        profile.DocumentType = request.DocumentType;
        profile.DocumentNumber = request.DocumentNumber;
        profile.Phone = request.Phone;
        profile.Address = request.Address;
        profile.ContactEmail = request.ContactEmail;
        profile.EnrollmentMonth = request.EnrollmentMonth;
        profile.EnrollmentDate = request.EnrollmentDate;
        profile.OrderNumber = request.OrderNumber;
        profile.ScheduleSlot = request.ScheduleSlot;
        profile.ReceiptNumber = request.ReceiptNumber;
        profile.AmountDue = request.AmountDue;
        profile.AmountPaid = request.AmountPaid;
        profile.BalanceDue = Math.Max(0, request.AmountDue - request.AmountPaid);
        profile.PaymentMethod = request.PaymentMethod;
        profile.BalancePaymentAmount = request.BalancePaymentAmount;
        profile.AccountsReceivable = request.AccountsReceivable;
        profile.BalancePaymentDate = request.BalancePaymentDate;
        profile.BalancePaymentMethod = request.BalancePaymentMethod;
        profile.BalanceReceiptNumber = request.BalanceReceiptNumber;
        profile.EnrollmentPin = request.EnrollmentPin;
        profile.RuntRegistered = request.RuntRegistered;
        profile.IsEnrolled = request.IsEnrolled;
        profile.Notes = request.Notes;
        profile.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        return MapDto(studentUserId, user.Name, user.Email ?? "", profile, enrollment);
    }

    public async Task<IReadOnlyList<TheoryExamSlotDto>> ListExamSlotsAsync(
        int schoolUserId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var end = to ?? start.AddDays(30);
        var slots = await _db.Set<TheoryExamAppointment>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.ExamDate >= start
                && x.ExamDate <= end)
            .OrderBy(x => x.ExamDate)
            .ThenBy(x => x.SlotTime)
            .ToListAsync(ct);

        var result = new List<TheoryExamSlotDto>();
        foreach (var slot in slots)
        {
            string? studentName = null;
            if (slot.StudentUserId is int sid)
            {
                var user = await _users.GetByIdAsync(sid, ct);
                studentName = user?.Name;
            }

            result.Add(new TheoryExamSlotDto(
                slot.Id,
                slot.ExamDate.ToString("yyyy-MM-dd"),
                slot.SlotTime.ToString("HH:mm"),
                slot.StudentUserId,
                slot.StudentLabel,
                studentName,
                slot.Notes));
        }

        return result;
    }

    public async Task<IReadOnlyList<TheoryExamSchedulingStudentDto>> ListTheoryExamStudentsAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var enrollments = await _db.Set<SchoolStudentEnrollment>()
            .Where(x => x.SchoolUserId == schoolUserId
                && x.TheoryExamAuthorized
                && StudentEnrollmentStatuses.CanReserveStatuses.Contains(x.Status))
            .ToListAsync(ct);

        var result = new List<TheoryExamSchedulingStudentDto>();
        foreach (var enrollment in enrollments)
        {
            var eligibility = await _theory.GetPracticalEligibilityAsync(
                schoolUserId,
                enrollment.StudentUserId,
                ct);
            if (eligibility.TheoryExamPassed)
            {
                continue;
            }

            if (!eligibility.TheoryHoursComplete || !eligibility.WorkshopHoursComplete)
            {
                continue;
            }

            var user = await _users.GetByIdAsync(enrollment.StudentUserId, ct);
            result.Add(new TheoryExamSchedulingStudentDto(
                enrollment.StudentUserId,
                user?.Name ?? $"Estudiante {enrollment.StudentUserId}",
                enrollment.LicenseCategories));
        }

        return result.OrderBy(x => x.StudentName).ToList();
    }

    public async Task<TheoryExamSlotDto> SaveExamSlotAsync(
        int schoolUserId,
        int? id,
        SaveTheoryExamSlotRequest request,
        CancellationToken ct)
    {
        await _membership.EnsureActiveAsync(schoolUserId, ct);
        var now = _clock.UtcNow;
        var start = ParseTime(request.SlotTime);

        if (request.StudentUserId is int studentId)
        {
            var enrollment = await _db.Set<SchoolStudentEnrollment>()
                .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                    && x.StudentUserId == studentId, ct)
                ?? throw new DomainException(
                    "El estudiante no está inscrito en la escuela.",
                    400,
                    "student_not_enrolled");

            if (!StudentEnrollmentStatuses.CanReserve.Contains(enrollment.Status))
            {
                throw new DomainException(
                    "El estudiante debe estar autorizado en Programación.",
                    400,
                    "student_not_authorized");
            }

            if (!enrollment.TheoryExamAuthorized)
            {
                throw new DomainException(
                    "El estudiante no está autorizado para examen teórico.",
                    400,
                    "theory_exam_not_authorized");
            }

            await _eligibility.EnsureNoBalanceDueAsync(schoolUserId, studentId, ct);

            var eligibility = await _theory.GetPracticalEligibilityAsync(
                schoolUserId,
                studentId,
                ct);
            if (eligibility.TheoryExamPassed)
            {
                throw new DomainException(
                    "El estudiante ya aprobó el examen teórico.",
                    400,
                    "theory_exam_already_passed");
            }

            if (!eligibility.TheoryHoursComplete || !eligibility.WorkshopHoursComplete)
            {
                throw new DomainException(
                    "El estudiante debe completar las horas de teoría y taller.",
                    400,
                    "theory_hours_incomplete");
            }

            var studentConflict = await _db.Set<TheoryExamAppointment>()
                .AnyAsync(x => x.SchoolUserId == schoolUserId
                    && x.StudentUserId == studentId
                    && x.ExamDate == request.ExamDate
                    && x.SlotTime == start
                    && (id == null || x.Id != id.Value), ct);
            if (studentConflict)
            {
                throw new DomainException(
                    "El estudiante ya tiene cita de examen en ese horario.",
                    400,
                    "exam_slot_conflict");
            }
        }

        TheoryExamAppointment entity;
        int? previousStudentId = null;
        if (id is > 0)
        {
            entity = await _db.Set<TheoryExamAppointment>()
                .FirstOrDefaultAsync(x => x.Id == id && x.SchoolUserId == schoolUserId, ct)
                ?? throw new NotFoundException("Cita no encontrada.", "slot_not_found");
            previousStudentId = entity.StudentUserId;
        }
        else
        {
            entity = new TheoryExamAppointment
            {
                SchoolUserId = schoolUserId,
                CreatedAt = now
            };
            await _db.Set<TheoryExamAppointment>().AddAsync(entity, ct);
        }

        entity.ExamDate = request.ExamDate;
        entity.SlotTime = start;
        entity.StudentUserId = request.StudentUserId;
        entity.StudentLabel = string.IsNullOrWhiteSpace(request.StudentLabel)
            ? null
            : request.StudentLabel.Trim();
        entity.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        entity.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        if (entity.StudentUserId is int assignedStudentId
            && assignedStudentId != previousStudentId)
        {
            await _notifications.NotifyUsersAsync(
                [assignedStudentId],
                new NotificationDraft(
                    "Cita de examen teórico",
                    $"Tu examen teórico está programado para el {entity.ExamDate:dd/MM/yyyy} a las {entity.SlotTime:HH:mm}.",
                    NotificationTypes.TheoryClass,
                    RelatedEntity: "theory_exam_appointment",
                    RelatedId: entity.Id,
                    Link: "/student/training"),
                ct);
        }

        string? studentName = null;
        if (entity.StudentUserId is int sid)
        {
            studentName = (await _users.GetByIdAsync(sid, ct))?.Name;
        }

        return new TheoryExamSlotDto(
            entity.Id,
            entity.ExamDate.ToString("yyyy-MM-dd"),
            entity.SlotTime.ToString("HH:mm"),
            entity.StudentUserId,
            entity.StudentLabel,
            studentName,
            entity.Notes);
    }

    public async Task DeleteExamSlotAsync(int schoolUserId, int id, CancellationToken ct)
    {
        await _membership.EnsureActiveAsync(schoolUserId, ct);
        var entity = await _db.Set<TheoryExamAppointment>()
            .FirstOrDefaultAsync(x => x.Id == id && x.SchoolUserId == schoolUserId, ct)
            ?? throw new NotFoundException("Cita no encontrada.", "slot_not_found");
        _db.Remove(entity);
        await _db.SaveChangesAsync(ct);
    }

    private static ApprenticeDto MapDto(
        int studentUserId,
        string studentName,
        string studentEmail,
        SchoolApprenticeProfile? profile,
        SchoolStudentEnrollment? enrollment) =>
        new(
            profile?.Id ?? 0,
            studentUserId,
            studentName,
            studentEmail,
            profile?.DocumentType,
            profile?.DocumentNumber,
            profile?.Phone,
            profile?.Address,
            profile?.ContactEmail,
            profile?.EnrollmentMonth,
            profile?.EnrollmentDate,
            profile?.OrderNumber,
            enrollment?.LicenseCategories,
            enrollment?.AttendanceDayType,
            profile?.ScheduleSlot,
            profile?.ReceiptNumber,
            profile?.AmountDue ?? 0,
            profile?.AmountPaid ?? 0,
            profile?.BalanceDue ?? 0,
            profile?.PaymentMethod,
            profile?.BalancePaymentAmount,
            profile?.AccountsReceivable ?? 0,
            profile?.BalancePaymentDate,
            profile?.BalancePaymentMethod,
            profile?.BalanceReceiptNumber,
            profile?.EnrollmentPin,
            profile?.RuntRegistered ?? false,
            profile?.IsEnrolled ?? false,
            enrollment?.Status ?? StudentEnrollmentStatuses.Pending,
            enrollment?.TheoryExamAuthorized ?? false,
            enrollment?.PracticalAuthorized ?? false,
            profile?.Notes);

    private async Task<bool> BelongsToSchoolAsync(
        int schoolUserId,
        int studentUserId,
        User user,
        CancellationToken ct)
    {
        if (user.Role != Roles.Student)
        {
            return false;
        }

        if (user.SchoolId == schoolUserId)
        {
            return true;
        }

        var hasProfile = await _db.Set<SchoolApprenticeProfile>()
            .AnyAsync(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId, ct);
        if (hasProfile)
        {
            return true;
        }

        return await _db.Set<SchoolStudentEnrollment>()
            .AnyAsync(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId, ct);
    }

    private static TimeOnly ParseTime(string value)
    {
        if (TimeOnly.TryParse(value.Trim(), out var time))
        {
            return time;
        }

        throw new DomainException("La hora no es válida.", 400, "invalid_time");
    }
}
