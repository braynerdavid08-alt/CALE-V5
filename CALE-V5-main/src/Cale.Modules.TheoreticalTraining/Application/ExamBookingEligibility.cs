using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.TheoreticalTraining.Application;

/// <summary>Who may hold a theory-exam appointment (same rules for school, student and Excel import).</summary>
public interface IExamBookingEligibility
{
    /// <summary>Throws a <see cref="DomainException"/> explaining why the student can't be booked.</summary>
    Task EnsureEligibleAsync(int schoolUserId, int studentUserId, CancellationToken ct);
}

public sealed class ExamBookingEligibility : IExamBookingEligibility
{
    private readonly CaleDbContext _db;
    private readonly ITrainingEligibilityService _eligibility;
    private readonly TheoryTrainingService _theory;

    public ExamBookingEligibility(
        CaleDbContext db,
        ITrainingEligibilityService eligibility,
        TheoryTrainingService theory)
    {
        _db = db;
        _eligibility = eligibility;
        _theory = theory;
    }

    public async Task EnsureEligibleAsync(int schoolUserId, int studentUserId, CancellationToken ct)
    {
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId, ct)
            ?? throw new DomainException(
                "El estudiante no está inscrito en la escuela.",
                400,
                "student_not_enrolled");

        if (!StudentEnrollmentStatuses.CanReserve.Contains(enrollment.Status))
        {
            throw new DomainException(
                "El estudiante debe estar activo en Clases teóricas.",
                400,
                "student_not_authorized");
        }

        if (!enrollment.TheoryExamAuthorized)
        {
            throw new DomainException(
                "El estudiante todavía no está autorizado para el examen teórico.",
                400,
                "theory_exam_not_authorized");
        }

        await _eligibility.EnsureNoBalanceDueAsync(schoolUserId, studentUserId, ct);

        var progress = await _theory.GetPracticalEligibilityAsync(schoolUserId, studentUserId, ct);
        if (progress.TheoryExamPassed)
        {
            throw new DomainException(
                "El estudiante ya aprobó el examen teórico.",
                400,
                "theory_exam_already_passed");
        }

        if (!progress.TheoryHoursComplete || !progress.WorkshopHoursComplete)
        {
            throw new DomainException(
                "El estudiante debe completar las horas de teoría y taller.",
                400,
                "theory_hours_incomplete");
        }
    }
}
