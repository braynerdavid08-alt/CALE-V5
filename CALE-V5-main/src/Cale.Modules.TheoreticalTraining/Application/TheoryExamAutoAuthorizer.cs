using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Engagement;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Domain.Time;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Assessment.Domain;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cale.Modules.TheoreticalTraining.Application;

/// <summary>
/// Grants the theory-exam authorization on its own once a student meets what exam booking
/// requires (active enrollment, theory + workshop hours complete, no balance due, exam not
/// passed yet). Booking a slot does not need the official platform exam configured, so
/// neither does this.
/// Only fires for students with no authorization history: if the school ever granted or
/// revoked it by hand, that decision stands.
/// </summary>
public sealed class TheoryExamAutoAuthorizer
{
    private readonly CaleDbContext _db;
    private readonly IClock _clock;
    private readonly ISchoolMembershipGuard _membership;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<TheoryExamAutoAuthorizer> _logger;

    public TheoryExamAutoAuthorizer(
        CaleDbContext db,
        IClock clock,
        ISchoolMembershipGuard membership,
        INotificationPublisher notifications,
        ILogger<TheoryExamAutoAuthorizer> logger)
    {
        _db = db;
        _clock = clock;
        _membership = membership;
        _notifications = notifications;
        _logger = logger;
    }

    /// <summary>Returns true when the authorization was granted by this call.</summary>
    public async Task<bool> TryAuthorizeAsync(int schoolUserId, int studentUserId, CancellationToken ct)
    {
        try
        {
            return await TryAuthorizeCoreAsync(schoolUserId, studentUserId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never break the action that triggered the check (attendance, payment, dashboard).
            _logger.LogWarning(ex, "Auto theory-exam authorization failed for student {StudentUserId}", studentUserId);
            return false;
        }
    }

    private async Task<bool> TryAuthorizeCoreAsync(int schoolUserId, int studentUserId, CancellationToken ct)
    {
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .Where(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
        if (enrollment is null
            || enrollment.TheoryExamAuthorized
            || !StudentEnrollmentStatuses.CanReserveStatuses.Contains(enrollment.Status))
        {
            return false;
        }

        var hasHistory = await _db.Set<EnrollmentAuthorizationEvent>()
            .AnyAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId
                && x.AuthorizationType == EnrollmentAuthorizationTypes.TheoryExam, ct);
        if (hasHistory)
        {
            return false;
        }

        var settings = await _db.Set<TheoryTrainingSettings>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId, ct)
            ?? new TheoryTrainingSettings { SchoolUserId = schoolUserId };

        var balanceDue = await _db.Set<SchoolApprenticeProfile>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId)
            .Select(x => (decimal?)x.BalanceDue)
            .FirstOrDefaultAsync(ct) ?? 0;
        if (balanceDue > 0)
        {
            return false;
        }

        var hours = await TrainingHoursCalculator.ComputeAsync(_db, schoolUserId, studentUserId, ct);
        var (requiredTheory, requiredWorkshop) = LicenseCategoryPolicyHelper.ResolveHourRequirements(
            settings,
            enrollment.LicenseCategories);
        if (hours.TheoryTotal < requiredTheory || hours.WorkshopTotal < requiredWorkshop)
        {
            return false;
        }

        var passed = settings.TheoryExamId is int examId
            && await _db.Set<Attempt>()
                .AnyAsync(a => a.UserId == studentUserId
                    && a.ExamId == examId
                    && a.FinishedAt != null
                    && a.Passed, ct);
        if (passed)
        {
            return false;
        }

        try
        {
            await _membership.EnsureActiveAsync(schoolUserId, ct);
        }
        catch (DomainException)
        {
            return false;
        }

        var now = _clock.UtcNow;
        enrollment.TheoryExamAuthorized = true;
        enrollment.TheoryExamAuthorizedAt = now;
        enrollment.UpdatedAt = now;
        await _db.Set<EnrollmentAuthorizationEvent>().AddAsync(new EnrollmentAuthorizationEvent
        {
            SchoolUserId = schoolUserId,
            StudentUserId = studentUserId,
            AuthorizationType = EnrollmentAuthorizationTypes.TheoryExam,
            Action = EnrollmentAuthorizationActions.Granted,
            PerformedByUserId = null,
            CreatedAt = now
        }, ct);
        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyUsersAsync(
            [studentUserId],
            new NotificationDraft(
                "¡Ya puedes agendar tu examen teórico!",
                "Completaste tus horas de teoría y taller. Quedaste habilitado automáticamente: entra y escoge la fecha de tu examen.",
                NotificationTypes.TheoryClass,
                RelatedEntity: "theory_exam_auth",
                RelatedId: enrollment.Id,
                Link: "/student/exam",
                DedupeKey: $"theory-exam-auto:{enrollment.Id}"),
            ct);
        return true;
    }
}
