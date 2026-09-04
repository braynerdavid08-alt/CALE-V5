using Cale.BuildingBlocks.Domain.Abstractions;
using Cale.BuildingBlocks.Domain.Exceptions;
using Cale.BuildingBlocks.Infrastructure.Persistence;
using Cale.Modules.Identity.Application.Abstractions;
using Cale.Modules.TheoreticalTraining.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cale.Modules.TheoreticalTraining.Application;

public sealed class TrainingEligibilityService : ITrainingEligibilityService
{
    private readonly CaleDbContext _db;
    private readonly IUserStore _users;

    public TrainingEligibilityService(CaleDbContext db, IUserStore users)
    {
        _db = db;
        _users = users;
    }

    public static bool CanStudentReserve(SchoolStudentEnrollment enrollment) =>
        StudentEnrollmentStatuses.CanReserveStatuses.Contains(enrollment.Status);

    public async Task EnsureStudentCanBookAsync(
        int schoolUserId,
        int studentUserId,
        CancellationToken ct)
    {
        var enrollment = await _db.Set<SchoolStudentEnrollment>()
            .FirstOrDefaultAsync(x => x.SchoolUserId == schoolUserId
                && x.StudentUserId == studentUserId, ct)
            ?? throw new DomainException(
                "El estudiante no está inscrito en la escuela.",
                400,
                "student_not_enrolled");

        if (enrollment.Status == StudentEnrollmentStatuses.Suspended)
        {
            throw new ForbiddenException(
                "Tu acceso está suspendido. Pide a la escuela que te autorice de nuevo.",
                "enrollment_suspended");
        }

        if (!CanStudentReserve(enrollment))
        {
            throw new ForbiddenException(
                "Tu escuela aún no te ha habilitado para reservar. Debes estar activo en Programación.",
                "enrollment_not_active");
        }
    }

    public async Task EnsureNoBalanceDueAsync(
        int schoolUserId,
        int studentUserId,
        CancellationToken ct)
    {
        var balance = await _db.Set<SchoolApprenticeProfile>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId)
            .Select(x => x.BalanceDue)
            .FirstOrDefaultAsync(ct);

        if (balance > 0)
        {
            throw new DomainException(
                "Hay saldo pendiente. Registra el pago en Aprendices antes de continuar.",
                400,
                "balance_due_pending");
        }
    }

    public async Task EnsureCanStartSchoolTheoryExamAsync(
        int studentUserId,
        int examId,
        CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(studentUserId, ct);
        if (user?.SchoolId is not int schoolUserId)
        {
            return;
        }

        var theoryExamId = await _db.Set<TheoryTrainingSettings>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId)
            .Select(x => x.TheoryExamId)
            .FirstOrDefaultAsync(ct);

        if (theoryExamId != examId)
        {
            return;
        }

        await EnsureTheoryExamConfiguredAsync(schoolUserId, ct);
        await EnsureStudentCanBookAsync(schoolUserId, studentUserId, ct);
        await EnsureNoBalanceDueAsync(schoolUserId, studentUserId, ct);

        var authorized = await _db.Set<SchoolStudentEnrollment>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId && x.StudentUserId == studentUserId)
            .Select(x => x.TheoryExamAuthorized)
            .FirstOrDefaultAsync(ct);

        if (!authorized)
        {
            throw new ForbiddenException(
                "Tu escuela debe autorizarte para presentar el examen teórico.",
                "exam_not_authorized");
        }
    }

    public async Task<int?> GetSchoolOfficialTheoryExamIdAsync(
        int studentUserId,
        CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(studentUserId, ct);
        if (user?.SchoolId is not int schoolUserId)
        {
            return null;
        }

        return await _db.Set<TheoryTrainingSettings>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId)
            .Select(x => x.TheoryExamId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task EnsureTheoryExamConfiguredAsync(
        int schoolUserId,
        CancellationToken ct)
    {
        var theoryExamId = await _db.Set<TheoryTrainingSettings>()
            .AsNoTracking()
            .Where(x => x.SchoolUserId == schoolUserId)
            .Select(x => x.TheoryExamId)
            .FirstOrDefaultAsync(ct);

        if (theoryExamId is null)
        {
            throw new DomainException(
                "Configura el examen teórico oficial en Ajustes antes de continuar.",
                400,
                "theory_exam_not_configured");
        }
    }
}
