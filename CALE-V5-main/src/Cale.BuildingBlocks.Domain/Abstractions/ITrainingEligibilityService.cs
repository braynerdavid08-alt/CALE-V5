namespace Cale.BuildingBlocks.Domain.Abstractions;

/// <summary>
/// Central policy checks for school training bookings and platform exams.
/// </summary>
public interface ITrainingEligibilityService
{
    Task EnsureStudentCanBookAsync(
        int schoolUserId,
        int studentUserId,
        CancellationToken ct = default);

    Task EnsureNoBalanceDueAsync(
        int schoolUserId,
        int studentUserId,
        CancellationToken ct = default);

    /// <summary>
    /// No-op when the exam is not the school's official theory exam.
    /// </summary>
    Task EnsureCanStartSchoolTheoryExamAsync(
        int studentUserId,
        int examId,
        CancellationToken ct = default);

    /// <summary>
    /// Official theory exam id for the student's school, if configured.
    /// </summary>
    Task<int?> GetSchoolOfficialTheoryExamIdAsync(
        int studentUserId,
        CancellationToken ct = default);

    Task EnsureTheoryExamConfiguredAsync(
        int schoolUserId,
        CancellationToken ct = default);
}
