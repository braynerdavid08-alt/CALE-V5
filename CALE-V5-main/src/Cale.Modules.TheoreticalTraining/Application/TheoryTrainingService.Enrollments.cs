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
    // ── Enrollments ─────────────────────────────────────────────────────

    public async Task<IReadOnlyList<EnrollmentDto>> ListEnrollmentsAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        try
        {
            return await ListEnrollmentsCoreAsync(schoolUserId, ct);
        }
        catch (Exception ex)
        {
            if (!AllowRequestPathRepair)
            {
                throw;
            }

            _logger.LogError(
                ex,
                "Enrollment list failed for school {SchoolUserId}; repairing and retrying",
                schoolUserId);
            await FeatureSchema.EnsureTheoryTrainingColumnsAsync(_db, ct);
            _db.ChangeTracker.Clear();
            return await ListEnrollmentsCoreAsync(schoolUserId, ct);
        }
    }

    private async Task<IReadOnlyList<EnrollmentDto>> ListEnrollmentsCoreAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        var students = (await _users.ListBySchoolAsync(schoolUserId, ct))
            .Where(x => x.Role == Roles.Student)
            .OrderBy(x => x.Name)
            .ToList();
        var items = (await _db.Set<SchoolStudentEnrollment>()
                .Where(x => x.SchoolUserId == schoolUserId)
                .ToListAsync(ct))
            .GroupBy(x => x.StudentUserId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First());
        var balances = (await _db.Set<SchoolApprenticeProfile>()
                .Where(x => x.SchoolUserId == schoolUserId)
                .ToListAsync(ct))
            .GroupBy(x => x.StudentUserId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Id).First().BalanceDue);

        var result = new List<EnrollmentDto>();
        foreach (var student in students)
        {
            var (theoryHours, workshopHours, _) = await ComputeHoursBreakdownAsync(
                schoolUserId,
                student.Id,
                ct);
            items.TryGetValue(student.Id, out var enrollmentRow);
            var eligibility = await GetPracticalEligibilityAsync(
                schoolUserId,
                student.Id,
                settings,
                theoryHours,
                workshopHours,
                enrollmentRow?.TheoryExamAuthorized ?? false,
                enrollmentRow?.PracticalAuthorized ?? false,
                enrollmentRow?.LicenseCategories,
                ct);
            balances.TryGetValue(student.Id, out var balanceDue);
            if (items.TryGetValue(student.Id, out var e))
            {
                result.Add(MapEnrollmentDto(e, student.Name, student.Email ?? "", eligibility, balanceDue));
            }
            else
            {
                result.Add(new EnrollmentDto(
                    0,
                    student.Id,
                    student.Name,
                    student.Email ?? "",
                    StudentEnrollmentStatuses.Pending,
                    null,
                    null,
                    null,
                    false,
                    false,
                    DateTime.UtcNow,
                    null,
                    eligibility,
                    balanceDue));
            }
        }

        return result;
    }

    public async Task<EnrollmentDto> UpdateEnrollmentAsync(
        int schoolUserId,
        int studentUserId,
        UpdateEnrollmentRequest request,
        int? actorUserId,
        CancellationToken ct)
    {
        await EnsureSchoolMembershipActiveAsync(schoolUserId, ct);
        var student = (await _users.ListBySchoolAsync(schoolUserId, ct))
            .FirstOrDefault(x => x.Id == studentUserId && x.Role == Roles.Student)
            ?? throw new NotFoundException("Estudiante no encontrado.", "student_not_found");

        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId, ct);

        var now = _clock.UtcNow;
        if (enrollment is null)
        {
            enrollment = new SchoolStudentEnrollment
            {
                SchoolUserId = schoolUserId,
                StudentUserId = studentUserId,
                CreatedAt = now
            };
            await _db.Set<SchoolStudentEnrollment>().AddAsync(enrollment, ct);
        }

        enrollment.Status = request.Status;
        enrollment.UpdatedAt = now;

        if (!string.IsNullOrWhiteSpace(request.AttendanceDayType))
        {
            var dayType = request.AttendanceDayType.Trim();
            if (!StudentAttendanceDayTypes.IsValid(dayType))
            {
                throw new DomainException("Tipo de día no válido.", 400, "invalid_day_type");
            }

            ActivateSchedulingGroup(settings, dayType);
            enrollment.AttendanceDayType = dayType;
        }

        if (request.LicenseCategories is not null)
        {
            var categories = request.LicenseCategories.Trim();
            if (categories.Length == 0)
            {
                enrollment.LicenseCategories = null;
            }
            else if (!StudentLicenseCategories.IsValid(categories))
            {
                throw new DomainException("Categoría de licencia no válida.", 400, "invalid_license_category");
            }
            else
            {
                enrollment.LicenseCategories = StudentLicenseCategories.Presets
                    .First(p => p.Equals(categories, StringComparison.OrdinalIgnoreCase));
            }
        }

        if (request.Status is StudentEnrollmentStatuses.Accepted or StudentEnrollmentStatuses.Active)
        {
            enrollment.AcceptedAt ??= now;
            if (enrollment.AttendanceDayType is null)
            {
                throw new DomainException(
                    "Indica si el estudiante asiste en Semana o los sábados.",
                    400,
                    "day_type_required");
            }

            if (string.IsNullOrWhiteSpace(enrollment.LicenseCategories))
            {
                throw new DomainException(
                    "Indica la categoría de licencia que cursa el estudiante.",
                    400,
                    "license_category_required");
            }
        }

        if (request.Status == StudentEnrollmentStatuses.Suspended)
        {
            enrollment.SuspendedAt = now;
        }
        else if (StudentEnrollmentStatuses.CanReserveStatuses.Contains(enrollment.Status))
        {
            enrollment.SuspendedAt = null;
            enrollment.AcceptedAt ??= now;
        }
        else if (!string.IsNullOrWhiteSpace(enrollment.AttendanceDayType)
            && !string.IsNullOrWhiteSpace(enrollment.LicenseCategories)
            && enrollment.Status == StudentEnrollmentStatuses.Pending)
        {
            enrollment.Status = StudentEnrollmentStatuses.Active;
            enrollment.AcceptedAt ??= now;
            enrollment.SuspendedAt = null;
        }

        var (theoryHours, workshopHours, _) = await ComputeHoursBreakdownAsync(
            schoolUserId,
            studentUserId,
            ct);
        var notifyTheoryExam = false;
        var notifyPractical = false;
        var prevTheoryAuth = enrollment.TheoryExamAuthorized;
        var prevPracticalAuth = enrollment.PracticalAuthorized;

        if (request.TheoryExamAuthorized is bool theoryExamAuthorized)
        {
            if (theoryExamAuthorized && !enrollment.TheoryExamAuthorized)
            {
                await _eligibility.EnsureTheoryExamConfiguredAsync(schoolUserId, ct);

                // School may override hours/balance when the counter is wrong;
                // only block if the student already passed the official exam.
                var hoursCheck = await GetPracticalEligibilityAsync(
                    schoolUserId,
                    studentUserId,
                    settings,
                    theoryHours,
                    workshopHours,
                    enrollment.TheoryExamAuthorized,
                    enrollment.PracticalAuthorized,
                    enrollment.LicenseCategories,
                    ct);
                if (hoursCheck.TheoryExamPassed)
                {
                    throw new DomainException(
                        "El estudiante ya aprobó el examen teórico.",
                        400,
                        "theory_exam_already_passed");
                }

                notifyTheoryExam = true;
            }

            enrollment.TheoryExamAuthorized = theoryExamAuthorized;
            enrollment.TheoryExamAuthorizedAt = theoryExamAuthorized ? now : null;
        }

        if (request.PracticalAuthorized is bool practicalAuthorized)
        {
            if (practicalAuthorized && !enrollment.PracticalAuthorized)
            {
                await _eligibility.EnsureTheoryExamConfiguredAsync(schoolUserId, ct);
                // Manual override allowed: school confirms even if hours/exam gate disagree.
                notifyPractical = true;
            }

            enrollment.PracticalAuthorized = practicalAuthorized;
            enrollment.PracticalAuthorizedAt = practicalAuthorized ? now : null;
        }

        if (request.TheoryExamAuthorized is bool theoryFlag
            && theoryFlag != prevTheoryAuth)
        {
            await LogAuthorizationEventAsync(
                schoolUserId,
                studentUserId,
                EnrollmentAuthorizationTypes.TheoryExam,
                theoryFlag ? EnrollmentAuthorizationActions.Granted : EnrollmentAuthorizationActions.Revoked,
                actorUserId,
                ct);
        }

        if (request.PracticalAuthorized is bool practicalFlag
            && practicalFlag != prevPracticalAuth)
        {
            await LogAuthorizationEventAsync(
                schoolUserId,
                studentUserId,
                EnrollmentAuthorizationTypes.Practical,
                practicalFlag ? EnrollmentAuthorizationActions.Granted : EnrollmentAuthorizationActions.Revoked,
                actorUserId,
                ct);
        }

        await _db.SaveChangesAsync(ct);

        if (notifyTheoryExam)
        {
            await NotifyTheoryExamAuthorizedAsync(studentUserId, enrollment.Id, ct);
        }

        if (notifyPractical)
        {
            await NotifyPracticalAuthorizedAsync(studentUserId, enrollment.Id, ct);
        }
        var eligibility = await GetPracticalEligibilityAsync(
            schoolUserId,
            studentUserId,
            settings,
            theoryHours,
            workshopHours,
            enrollment.TheoryExamAuthorized,
            enrollment.PracticalAuthorized,
            enrollment.LicenseCategories,
            ct);
        var balanceDue = await GetBalanceDueAsync(schoolUserId, studentUserId, ct);
        return MapEnrollmentDto(enrollment, student.Name, student.Email ?? "", eligibility, balanceDue);
    }

    public async Task<BulkAuthorizeEnrollmentsResultDto> BulkAuthorizeEnrollmentsAsync(
        int schoolUserId,
        BulkAuthorizeEnrollmentsRequest request,
        int? actorUserId,
        CancellationToken ct)
    {
        await EnsureSchoolMembershipActiveAsync(schoolUserId, ct);
        if (!request.TheoryExam && !request.Practical)
        {
            throw new DomainException("Indica qué autorización aplicar.", 400, "invalid_request");
        }

        var settings = await GetOrCreateSettingsAsync(schoolUserId, ct);
        var enrollments = await _db.Set<SchoolStudentEnrollment>()
            .Where(x => x.SchoolUserId == schoolUserId)
            .ToListAsync(ct);

        var now = _clock.UtcNow;
        var authorized = 0;
        var skipped = 0;
        var skippedInactive = 0;
        var skippedBalanceDue = 0;
        var skippedAlreadyAuthorized = 0;
        var skippedHoursIncomplete = 0;
        var skippedExamPassed = 0;
        var skippedExamNotPassed = 0;
        var skippedAlreadyPractical = 0;
        var skippedTheoryExamNotConfigured = 0;
        var theoryExamNotified = new List<(int StudentUserId, int EnrollmentId)>();
        var practicalNotified = new List<(int StudentUserId, int EnrollmentId)>();

        foreach (var enrollment in enrollments)
        {
            if (!StudentEnrollmentStatuses.CanReserveStatuses.Contains(enrollment.Status))
            {
                skipped++;
                skippedInactive++;
                continue;
            }

            if (await GetBalanceDueAsync(schoolUserId, enrollment.StudentUserId, ct) > 0)
            {
                skipped++;
                skippedBalanceDue++;
                continue;
            }

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

            if (request.TheoryExam)
            {
                if (settings.TheoryExamId is null)
                {
                    skipped++;
                    skippedTheoryExamNotConfigured++;
                }
                else if (enrollment.TheoryExamAuthorized)
                {
                    skipped++;
                    skippedAlreadyAuthorized++;
                }
                else if (!eligibility.TheoryHoursComplete || !eligibility.WorkshopHoursComplete)
                {
                    skipped++;
                    skippedHoursIncomplete++;
                }
                else if (eligibility.TheoryExamPassed)
                {
                    skipped++;
                    skippedExamPassed++;
                }
                else
                {
                    enrollment.TheoryExamAuthorized = true;
                    enrollment.TheoryExamAuthorizedAt = now;
                    enrollment.UpdatedAt = now;
                    await LogAuthorizationEventAsync(
                        schoolUserId,
                        enrollment.StudentUserId,
                        EnrollmentAuthorizationTypes.TheoryExam,
                        EnrollmentAuthorizationActions.Granted,
                        actorUserId,
                        ct);
                    theoryExamNotified.Add((enrollment.StudentUserId, enrollment.Id));
                    authorized++;
                }
            }

            if (!request.Practical)
            {
                continue;
            }

            if (enrollment.PracticalAuthorized)
            {
                skipped++;
                skippedAlreadyPractical++;
                continue;
            }

            if (!eligibility.TheoryExamPassed)
            {
                skipped++;
                skippedExamNotPassed++;
                continue;
            }

            enrollment.PracticalAuthorized = true;
            enrollment.PracticalAuthorizedAt = now;
            enrollment.UpdatedAt = now;
            await LogAuthorizationEventAsync(
                schoolUserId,
                enrollment.StudentUserId,
                EnrollmentAuthorizationTypes.Practical,
                EnrollmentAuthorizationActions.Granted,
                actorUserId,
                ct);
            practicalNotified.Add((enrollment.StudentUserId, enrollment.Id));
            authorized++;
        }

        await _db.SaveChangesAsync(ct);

        foreach (var (studentUserId, enrollmentId) in theoryExamNotified)
        {
            await NotifyTheoryExamAuthorizedAsync(studentUserId, enrollmentId, ct);
        }

        foreach (var (studentUserId, enrollmentId) in practicalNotified)
        {
            await NotifyPracticalAuthorizedAsync(studentUserId, enrollmentId, ct);
        }

        return new BulkAuthorizeEnrollmentsResultDto(
            authorized,
            skipped,
            skippedInactive,
            skippedBalanceDue,
            skippedAlreadyAuthorized,
            skippedHoursIncomplete,
            skippedExamPassed,
            skippedExamNotPassed,
            skippedAlreadyPractical,
            skippedTheoryExamNotConfigured);
    }

    public async Task<EnrollmentDto> UpdateEnrollmentByIdAsync(
        int schoolUserId,
        int enrollmentId,
        UpdateEnrollmentRequest request,
        int? actorUserId,
        CancellationToken ct)
    {
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.Id == enrollmentId && x.SchoolUserId == schoolUserId, ct)
            ?? throw new NotFoundException("Inscripción no encontrada.", "enrollment_not_found");

        return await UpdateEnrollmentAsync(schoolUserId, enrollment.StudentUserId, request, actorUserId, ct);
    }
}
