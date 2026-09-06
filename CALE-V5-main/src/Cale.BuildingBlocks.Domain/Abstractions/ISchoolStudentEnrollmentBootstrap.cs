namespace Cale.BuildingBlocks.Domain.Abstractions;

/// <summary>
/// Ensures a school-linked student has a Pending enrollment row for formación.
/// </summary>
public interface ISchoolStudentEnrollmentBootstrap
{
    Task EnsurePendingAsync(
        int schoolUserId,
        int studentUserId,
        CancellationToken ct = default,
        StudentOnboardingSeed? seed = null);
}
